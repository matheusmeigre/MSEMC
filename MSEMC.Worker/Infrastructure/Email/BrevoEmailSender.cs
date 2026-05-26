using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using MSEMC.Abstractions;
using MSEMC.Configuration;
using MSEMC.Domain.Entities;
using MSEMC.Domain.Results;

namespace MSEMC.Infrastructure.Email;

public sealed class BrevoEmailSender(
    IHttpClientFactory httpClientFactory,
    IOptions<BrevoOptions> options,
    ILogger<BrevoEmailSender> logger) : IEmailSender
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly BrevoOptions _options = options.Value;

    public async Task<Result<EmailMessage>> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Preparando e-mail para {Recipient} com assunto '{Subject}' (MessageId: {MessageId})",
            message.Recipient, message.Subject, message.Id);

        try
        {
            message.MarkAsSending();

            // Monta attachments no formato esperado pela API Brevo
            object[]? attachments = message.Attachments.Count > 0
                ? message.Attachments
                    .Select(a => (object)new { name = a.FileName, content = a.ContentBase64 })
                    .ToArray()
                : null;

            var payload = new
            {
                sender = new { name = _options.SenderDisplayName, email = _options.SenderEmail },
                to = new[] { new { email = message.Recipient } },
                cc = message.CcRecipients.Count > 0
                    ? message.CcRecipients.Select(e => new { email = e }).ToArray()
                    : null,
                bcc = message.BccRecipients.Count > 0
                    ? message.BccRecipients.Select(e => new { email = e }).ToArray()
                    : null,
                subject = message.Subject,
                htmlContent = message.IsHtml ? message.Body : null,
                textContent = message.IsHtml ? null : message.Body,
                attachment = attachments
            };

            var client = httpClientFactory.CreateClient("brevo");

            using var request = new HttpRequestMessage(HttpMethod.Post, "smtp/email");
            request.Headers.Add("api-key", _options.ApiKey);
            request.Content = JsonContent.Create(payload, options: JsonOptions);

            using var response = await client.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError(
                    "API Brevo retornou {StatusCode} para {Recipient} (MessageId: {MessageId}): {Error}",
                    (int)response.StatusCode, message.Recipient, message.Id, error);

                message.MarkAsFailed($"Brevo API error {(int)response.StatusCode}: {error}");
                return Result<EmailMessage>.Fail($"Email delivery failed: HTTP {(int)response.StatusCode}");
            }

            message.MarkAsSent();

            logger.LogInformation(
                "E-mail entregue com sucesso para {Recipient} (MessageId: {MessageId}, EnviadoEm: {SentAt})",
                message.Recipient, message.Id, message.SentAt);

            return Result<EmailMessage>.Ok(message);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Envio de e-mail cancelado para {Recipient} (MessageId: {MessageId})",
                message.Recipient, message.Id);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Falha ao enviar e-mail para {Recipient} (MessageId: {MessageId})",
                message.Recipient, message.Id);

            message.MarkAsFailed(ex.Message);
            return Result<EmailMessage>.Fail($"Email delivery failed: {ex.Message}");
        }
    }
}
