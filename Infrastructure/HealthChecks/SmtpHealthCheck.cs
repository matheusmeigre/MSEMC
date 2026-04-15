using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using MSEMC.Configuration;

namespace MSEMC.Infrastructure.HealthChecks;

/// <summary>
/// Health check que verifica a conectividade com o servidor SMTP.
/// Testa conexão e autenticação sem enviar nenhum e-mail.
/// </summary>
public sealed class SmtpHealthCheck(
    IOptions<SmtpOptions> options,
    ILogger<SmtpHealthCheck> logger) : IHealthCheck
{
    private readonly SmtpOptions _options = options.Value;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var client = new SmtpClient();

            await client.ConnectAsync(
                _options.Host,
                _options.Port,
                _options.EnableSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None,
                cancellationToken);

            await client.AuthenticateAsync(
                _options.Username,
                _options.Password,
                cancellationToken);

            await client.DisconnectAsync(quit: true, cancellationToken);

            return HealthCheckResult.Healthy("SMTP connection successful");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMTP health check failed for {Host}:{Port}", _options.Host, _options.Port);

            return HealthCheckResult.Unhealthy(
                description: $"SMTP connection failed: {ex.Message}",
                exception: ex,
                data: new Dictionary<string, object>
                {
                    ["host"] = _options.Host,
                    ["port"] = _options.Port
                });
        }
    }
}
