namespace MSEMC.Contracts.Requests;

/// <summary>
/// API contract for sending an email message.
/// Immutable record used as the HTTP request body DTO.
/// </summary>
public sealed record SendEmailRequest(
    string Recipient,
    string Subject,
    string Body,
    bool IsHtml = true,
    List<string>? CcRecipients = null,
    List<string>? BccRecipients = null
);
