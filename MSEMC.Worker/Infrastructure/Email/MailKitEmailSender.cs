using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MSEMC.Abstractions;
using MSEMC.Configuration;
using MSEMC.Domain.Entities;
using MSEMC.Domain.Results;
using MSEMC.Infrastructure.Resilience;
using Polly.Registry;

namespace MSEMC.Infrastructure.Email;

/// <summary>
/// Implementação de envio de e-mail usando MailKit com suporte a attachments.
/// </summary>
public sealed class MailKitEmailSender(
    IOptions<SmtpOptions> options,
    ResiliencePipelineProvider<string> pipelineProvider,
    ILogger<MailKitEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

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

            var mimeMessage = BuildMimeMessage(message);
            var pipeline = pipelineProvider.GetPipeline(ResiliencePolicies.SmtpPipelineName);

            await pipeline.ExecuteAsync(async ct =>
            {
                using var client = new SmtpClient();

                await client.ConnectAsync(
                    _options.Host,
                    _options.Port,
                    _options.EnableSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.None,
                    ct);

                await client.AuthenticateAsync(_options.Username, _options.Password, ct);
                await client.SendAsync(mimeMessage, ct);
                await client.DisconnectAsync(quit: true, ct);
            }, cancellationToken);

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
        catch (AuthenticationException ex)
        {
            logger.LogError(ex,
                "Falha de autenticação SMTP em {Host}:{Port} (MessageId: {MessageId})",
                _options.Host, _options.Port, message.Id);

            message.MarkAsFailed($"Authentication failed: {ex.Message}");
            return Result<EmailMessage>.Fail("SMTP authentication failed. Check credentials.");
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

    private MimeMessage BuildMimeMessage(EmailMessage message)
    {
        var mime = new MimeMessage();

        mime.From.Add(new MailboxAddress(_options.SenderDisplayName, _options.SenderEmail));
        mime.To.Add(MailboxAddress.Parse(message.Recipient));

        foreach (var cc in message.CcRecipients)
            mime.Cc.Add(MailboxAddress.Parse(cc));

        foreach (var bcc in message.BccRecipients)
            mime.Bcc.Add(MailboxAddress.Parse(bcc));

        mime.Subject = message.Subject;

        var builder = new BodyBuilder();

        if (message.IsHtml)
            builder.HtmlBody = message.Body;
        else
            builder.TextBody = message.Body;

        // Anexar attachments pré-gerados pelo serviço de origem
        foreach (var attachment in message.Attachments)
        {
            var contentType = ContentType.Parse(attachment.ContentType);
            builder.Attachments.Add(attachment.FileName, attachment.GetContentBytes(), contentType);
        }

        mime.Body = builder.ToMessageBody();

        return mime;
    }
}
