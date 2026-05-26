using MassTransit;
using MSEMC.Abstractions;
using MSEMC.Configuration;
using Serilog.Sinks.Grafana.Loki;
using MSEMC.Infrastructure.Email;
using MSEMC.Infrastructure.Resilience;
using MSEMC.Infrastructure.Telemetry;
using MSEMC.Infrastructure.Templates;
using MSEMC.Messaging.Consumers;
using MSEMC.Services;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddSerilog((sp, config) =>
    {
        var context = sp.GetRequiredService<IConfiguration>();
        var lokiOptions = context
            .GetSection(LokiOptions.SectionName)
            .Get<LokiOptions>();

        config
            .ReadFrom.Configuration(context)
            .ReadFrom.Services(sp)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        if (lokiOptions is { Enabled: true })
        {
            config.WriteTo.GrafanaLoki(
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

    builder.Services.AddOptions<SmtpOptions>()
        .BindConfiguration(SmtpOptions.SectionName)
        .ValidateDataAnnotations();

    builder.Services.AddOptions<BrevoOptions>()
        .BindConfiguration(BrevoOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    builder.Services.AddOptions<RabbitMqOptions>()
        .BindConfiguration(RabbitMqOptions.SectionName)
        .ValidateDataAnnotations();

    builder.Services.AddOptions<LokiOptions>()
        .BindConfiguration(LokiOptions.SectionName)
        .ValidateDataAnnotations();

    builder.Services.AddOptions<TemplateOptions>()
        .BindConfiguration(TemplateOptions.SectionName)
        .ValidateDataAnnotations();

    builder.Services.AddOptions<GovernanceOptions>()
        .BindConfiguration(GovernanceOptions.SectionName);

    builder.Services.AddSmtpResilience();

    var rabbitConfig = builder.Configuration
        .GetSection(RabbitMqOptions.SectionName)
        .Get<RabbitMqOptions>();

    builder.Services.AddMassTransit(bus =>
    {
        bus.AddConsumer<SendEmailConsumer>();
        bus.AddConsumer<SendLlmDigestConsumer>();

        if (rabbitConfig is not null && !string.IsNullOrWhiteSpace(rabbitConfig.Host))
        {
            bus.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitConfig.Host, rabbitConfig.Port, rabbitConfig.Username, h =>
                {
                    h.Username(rabbitConfig.Username);
                    h.Password(rabbitConfig.Password);
                });

                cfg.ReceiveEndpoint("send-email-queue", e =>
                {
                    e.ConfigureConsumer<SendEmailConsumer>(context);
                });

                cfg.ReceiveEndpoint("send-llm-digest-queue", e =>
                {
                    e.ConfigureConsumer<SendLlmDigestConsumer>(context);
                });
            });
        }
        else
        {
            bus.UsingInMemory((context, cfg) =>
            {
                cfg.ReceiveEndpoint("send-email-queue", e =>
                {
                    e.ConfigureConsumer<SendEmailConsumer>(context);
                });

                cfg.ReceiveEndpoint("send-llm-digest-queue", e =>
                {
                    e.ConfigureConsumer<SendLlmDigestConsumer>(context);
                });
            });
        }
    });

    builder.Services.AddHttpClient("brevo", client =>
        client.BaseAddress = new Uri("https://api.brevo.com/v3/"));

    builder.Services.AddScoped<IEmailSender, BrevoEmailSender>();

    builder.Services.AddMemoryCache();
    builder.Services.AddSingleton<ITemplateEngine, ScribanTemplateEngine>();
    builder.Services.AddSingleton<ITemplateLoader, FileSystemTemplateLoader>();
    builder.Services.AddSingleton<TemplateVariableValidator>();
    builder.Services.AddScoped<ITemplateRenderingService, TemplateRenderingService>();
    builder.Services.AddSingleton<IRecipientGovernanceService, RecipientGovernanceService>();

    builder.Services.AddSingleton(MsemcTelemetry.ActivitySource);

    var host = builder.Build();

    Log.Information("MSEMC.Worker iniciado. Aguardando mensagens do RabbitMQ...");

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MSEMC.Worker encerrado inesperadamente");
}
finally
{
    Log.CloseAndFlush();
}
