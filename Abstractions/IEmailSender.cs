using MSEMC.Domain.Entities;
using MSEMC.Domain.Results;

namespace MSEMC.Abstractions;

/// <summary>
/// Abstraction for sending email messages.
/// Enables Dependency Inversion — controllers depend on this interface,
/// not on concrete implementations (SmtpClient, MailKit, etc.).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Sends an email message asynchronously.
    /// Returns a Result indicating success or failure without throwing exceptions.
    /// </summary>
    /// <param name="message">The email message to send.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A Result containing the updated EmailMessage on success, or an error on failure.</returns>
    Task<Result<EmailMessage>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
