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
using MSEMC.Infrastructure.Email;
using MSEMC.Infrastructure.Resilience;
using MSEMC.Infrastructure.Telemetry;
using MSEMC.Messaging.Consumers;
using MSEMC.Messaging.Publishers;
using MSEMC.Middleware;
using MSEMC.Security;
using Serilog;

// ── Bootstrap Serilog (early init for startup error capture) ──
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog: structured logging from configuration ──
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

    // ── Configuration: Options Pattern with validation at startup ──
    builder.Services.AddOptions<SmtpOptions>()
        .BindConfiguration(SmtpOptions.SectionName)
        .ValidateDataAnnotations();

    builder.Services.AddOptions<BrevoOptions>()
        .BindConfiguration(BrevoOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

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

    // ── Authentication: API Key ──
    builder.Services.AddAuthentication(ApiKeyAuthenticationHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
            ApiKeyAuthenticationHandler.SchemeName, null);
    builder.Services.AddAuthorization();

    // ── Rate Limiting: Fixed Window ──
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

    // ── Resilience: Polly v8 pipelines ──
    builder.Services.AddSmtpResilience();

    // ── Messaging: MassTransit + RabbitMQ ──
    var rabbitConfig = builder.Configuration
        .GetSection(RabbitMqOptions.SectionName)
        .Get<RabbitMqOptions>();

    builder.Services.AddMassTransit(bus =>
    {
        bus.AddConsumer<SendEmailConsumer>();

        if (rabbitConfig is not null && !string.IsNullOrWhiteSpace(rabbitConfig.Host))
        {
            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitConfig.Host, rabbitConfig.Port, rabbitConfig.Username, h =>
                {
                    h.Username(rabbitConfig.Username);
                    h.Password(rabbitConfig.Password);
                });

                cfg.ConfigureEndpoints(context);
            });
        }
        else
        {
            // Fallback: InMemory transport for development without Docker
            bus.UsingInMemory((context, cfg) =>
            {
                cfg.ConfigureEndpoints(context);
            });
        }
    });

    // ── Brevo: HTTP email API (SMTP-free) ──
    builder.Services.AddHttpClient("brevo", client =>
        client.BaseAddress = new Uri("https://api.brevo.com/v3/"));

    // ── Dependency Injection ──
    builder.Services.AddScoped<IEmailSender, BrevoEmailSender>();
    builder.Services.AddScoped<IEmailQueuePublisher, MassTransitEmailPublisher>();

    // ── Validation: FluentValidation auto-discovery ──
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // ── Error Handling: RFC 7807 Problem Details ──
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // ── Health Checks ──
    builder.Services.AddHealthChecks();

    // ── OpenTelemetry: Custom Metrics ──
    builder.Services.AddSingleton(MsemcTelemetry.ActivitySource);

    // ── API: Swagger with API Key support ──
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

    // ── Middleware Pipeline (order matters) ──
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

    // ── Endpoints ──
    app.MapMessageEndpoints();

    // ── Health Checks Endpoints ──
    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        Predicate = _ => false // Liveness: always 200 if app is running
    }).AllowAnonymous();

    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"),
        ResponseWriter = WriteHealthCheckResponse
    }).AllowAnonymous();

    Log.Information("MSEMC starting on {Environment}", app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MSEMC terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// ── Health Check Response Writer ──
static Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json";

    var response = new
    {
        status = report.Status.ToString(),
        duration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds,
            data = e.Value.Data
        })
    };

    return context.Response.WriteAsJsonAsync(response);
}
