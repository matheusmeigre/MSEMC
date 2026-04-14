using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using MSEMC.Abstractions;
using MSEMC.Configuration;
using MSEMC.Domain.Entities;
using MSEMC.Domain.Results;

namespace MSEMC.Infrastructure.Email;

/// <summary>
/// Production-grade email sender using MailKit (replaces obsolete System.Net.Mail.SmtpClient).
/// Features: full async/await, proper TLS, CancellationToken propagation, structured logging.
/// </summary>
public sealed class MailKitEmailSender(
    IOptions<SmtpOptions> options,
    ILogger<MailKitEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task<Result<EmailMessage>> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Preparing email to {Recipient} with subject '{Subject}' (MessageId: {MessageId})",
            message.Recipient, message.Subject, message.Id);

        try
        {
            message.MarkAsSending();

            var mimeMessage = BuildMimeMessage(message);

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

            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);

            message.MarkAsSent();

            logger.LogInformation(
                "Email delivered successfully to {Recipient} (MessageId: {MessageId}, SentAt: {SentAt})",
                message.Recipient, message.Id, message.SentAt);

            return Result<EmailMessage>.Ok(message);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Email sending cancelled for {Recipient} (MessageId: {MessageId})",
                message.Recipient, message.Id);
            throw;
        }
        catch (AuthenticationException ex)
        {
            logger.LogError(ex,
                "SMTP authentication failed for {Host}:{Port} (MessageId: {MessageId})",
                _options.Host, _options.Port, message.Id);

            message.MarkAsFailed($"Authentication failed: {ex.Message}");
            return Result<EmailMessage>.Fail("SMTP authentication failed. Check credentials.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to send email to {Recipient} (MessageId: {MessageId})",
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
        {
            mime.Cc.Add(MailboxAddress.Parse(cc));
        }

        foreach (var bcc in message.BccRecipients)
        {
            mime.Bcc.Add(MailboxAddress.Parse(bcc));
        }

        mime.Subject = message.Subject;

        mime.Body = message.IsHtml
            ? new BodyBuilder { HtmlBody = message.Body }.ToMessageBody()
            : new TextPart("plain") { Text = message.Body };

        return mime;
    }
}
