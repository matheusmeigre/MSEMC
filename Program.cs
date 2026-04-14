using FluentValidation;
using MSEMC.Abstractions;
using MSEMC.Configuration;
using MSEMC.Infrastructure.Email;
using MSEMC.Infrastructure.Resilience;
using MSEMC.Middleware;
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
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"));

    // ── Configuration: Options Pattern with validation at startup ──
    builder.Services.AddOptions<SmtpOptions>()
        .BindConfiguration(SmtpOptions.SectionName)
        .ValidateDataAnnotations()
        .ValidateOnStart();

    // ── Resilience: Polly v8 pipelines ──
    builder.Services.AddSmtpResilience();

    // ── Dependency Injection ──
    builder.Services.AddScoped<IEmailSender, MailKitEmailSender>();

    // ── Validation: FluentValidation auto-discovery ──
    builder.Services.AddValidatorsFromAssemblyContaining<Program>();

    // ── Error Handling: RFC 7807 Problem Details ──
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // ── API: Controllers + Swagger ──
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

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
    app.UseAuthorization();
    app.MapControllers();

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
