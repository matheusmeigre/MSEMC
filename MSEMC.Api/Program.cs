using System.Threading.RateLimiting;
using FluentValidation;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using MSEMC.Abstractions;
using MSEMC.Configuration;
using MSEMC.Endpoints;
using Serilog.Sinks.Grafana.Loki;
using MSEMC.Infrastructure.Templates;
using MSEMC.Messaging.Publishers;
using MSEMC.Middleware;
using MSEMC.Security;
using MSEMC.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var railwayPort = Environment.GetEnvironmentVariable("PORT");
    if (!string.IsNullOrWhiteSpace(railwayPort))
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", $"http://+:{railwayPort}");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) =>
    {
        var lokiOptions = context.Configuration
            .GetSection(LokiOptions.SectionName)
            .Get<LokiOptions>();

        var serilogConfig = configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        if (lokiOptions is { Enabled: true })
        {
            serilogConfig.WriteTo.GrafanaLoki(
                uri: lokiOptions.Uri,
                labels:
                [
                    new LokiLabel { Key = "app", Value = lokiOptions.AppLabel },
                    new LokiLabel { Key = "environment", Value = lokiOptions.EnvironmentLabel }
                ],
                propertiesAsLabels: [],
                credentials: new LokiCredentials
                {
                    Login = lokiOptions.Username,
                    Password = lokiOptions.Password
                });
        }
    });

    builder.Services.AddOptions<ApiKeyOptions>()
        .BindConfiguration(ApiKeyOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<RateLimitOptions>()
        .BindConfiguration(RateLimitOptions.SectionName)
        .ValidateDataAnnotations();

    builder.Services.AddOptions<RabbitMqOptions>()
        .BindConfiguration(RabbitMqOptions.SectionName)
        .ValidateDataAnnotations();

    builder.Services.AddOptions<LokiOptions>()
        .BindConfiguration(LokiOptions.SectionName)
        .ValidateDataAnnotations();

    builder.Services.AddOptions<TemplateOptions>()
        .BindConfiguration(TemplateOptions.SectionName)
        .ValidateDataAnnotations();

    builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationHandler.SchemeName, null);
    builder.Services.AddAuthorization();

    var rateLimitConfig = builder.Configuration
        .GetSection(RateLimitOptions.SectionName)
        .Get<RateLimitOptions>() ?? new RateLimitOptions { PermitLimit = 100, WindowSeconds = 60 };

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddFixedWindowLimiter("messages", limiter =>
        {
            limiter.PermitLimit = rateLimitConfig.PermitLimit;
            limiter.Window = TimeSpan.FromSeconds(rateLimitConfig.WindowSeconds);
            limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            limiter.QueueLimit = 5;
        });
    });

    var rabbitConfig = builder.Configuration
        .GetSection(RabbitMqOptions.SectionName)
        .Get<RabbitMqOptions>();

    builder.Services.AddMassTransit(bus =>
    {
        if (rabbitConfig is not null && !string.IsNullOrWhiteSpace(rabbitConfig.Host))
        {
            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitConfig.Host, rabbitConfig.Port, rabbitConfig.Username, h =>
                {
                    h.Username(rabbitConfig.Username);
                    h.Password(rabbitConfig.Password);
                });
            });
        }
        else
        {
            bus.UsingInMemory();
        }
    });

    builder.Services.AddScoped<IEmailQueuePublisher, MassTransitEmailPublisher>();

    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
    builder.Services.AddSingleton<ITemplateLoader, FileSystemTemplateLoader>();
    builder.Services.AddSingleton<TemplateVariableValidator>();
    builder.Services.AddScoped<ITemplateRenderingService, TemplateRenderingService>();

    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddHealthChecks();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "MSEMC — API de Envio de Mensagens",
            Version = "v1",
            Description = "Microserviço para Envio de Mensagens aos Clientes. Autentique-se com sua API Key antes de utilizar os endpoints."
        });

        options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
        {
            Description = "Autenticação via API Key. Informe sua chave no campo abaixo.",
            Name = "X-API-Key",
            In = ParameterLocation.Header,
            Type = SecuritySchemeType.ApiKey,
            Scheme = "ApiKey"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "ApiKey"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    var app = builder.Build();

    app.UseExceptionHandler();
    app.UseMiddleware<RequestLoggingMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();

    app.MapMessageEndpoints();
    app.MapTemplateEndpoints();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => false
    }).AllowAnonymous();

    Log.Information("MSEMC.Api iniciando em {Environment}", app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MSEMC.Api encerrado inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}
