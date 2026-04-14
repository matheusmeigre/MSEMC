using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;
using MSEMC.Abstractions;
using MSEMC.Configuration;
using MSEMC.Domain.Entities;
using MSEMC.Domain.Results;

namespace MSEMC.Services;

/// <summary>
/// Sends emails via System.Net.Mail.SmtpClient with proper async/await and IDisposable.
/// Implements <see cref="IEmailSender"/> for dependency inversion.
///
/// Note: SmtpClient is marked obsolete by Microsoft. This implementation will be
/// replaced by MailKit in Phase 2 — Robustness.
/// </summary>
public sealed class EmailService : IEmailSender, IDisposable
{
    private readonly SmtpOptions _options;
    private readonly ILogger<EmailService> _logger;
    private readonly SmtpClient _smtpClient;
    private bool _disposed;

    public EmailService(IOptions<SmtpOptions> options, ILogger<EmailService> logger)
    {
        _options = options.Value;
        _logger = logger;

#pragma warning disable CS0618 // SmtpClient is obsolete — will be replaced in Phase 2
        _smtpClient = new SmtpClient(_options.Host)
        {
            Port = _options.Port,
            Credentials = new NetworkCredential(_options.Username, _options.Password),
            EnableSsl = _options.EnableSsl
        };
#pragma warning restore CS0618
    }

    public async Task<Result<EmailMessage>> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _logger.LogInformation(
            "Sending email to {Recipient} with subject '{Subject}' (MessageId: {MessageId})",
            message.Recipient, message.Subject, message.Id);

        try
        {
            message.MarkAsSending();

            var mailMessage = BuildMailMessage(message);

            await _smtpClient.SendMailAsync(mailMessage, cancellationToken);

            message.MarkAsSent();

            _logger.LogInformation(
                "Email sent successfully to {Recipient} (MessageId: {MessageId}, Duration: {SentAt})",
                message.Recipient, message.Id, message.SentAt);

            return Result<EmailMessage>.Ok(message);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Email sending cancelled for {Recipient} (MessageId: {MessageId})",
                message.Recipient, message.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send email to {Recipient} (MessageId: {MessageId})",
                message.Recipient, message.Id);

            message.MarkAsFailed(ex.Message);
            return Result<EmailMessage>.Fail($"Failed to send email: {ex.Message}");
        }
    }

    private MailMessage BuildMailMessage(EmailMessage message)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(_options.SenderEmail, _options.SenderDisplayName),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = message.IsHtml
        };

        mail.To.Add(message.Recipient);

        foreach (var cc in message.CcRecipients)
        {
            mail.CC.Add(cc);
        }

        foreach (var bcc in message.BccRecipients)
        {
            mail.Bcc.Add(bcc);
        }

        return mail;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _smtpClient.Dispose();
        _disposed = true;
    }
}
