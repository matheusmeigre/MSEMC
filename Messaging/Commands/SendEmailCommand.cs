namespace MSEMC.Messaging.Commands;

/// <summary>
/// MassTransit command to send an email asynchronously via the message queue.
/// Published by the API, consumed by the SendEmailConsumer.
/// </summary>
public sealed record SendEmailCommand(
    Guid MessageId,
    string Recipient,
    string Subject,
    string Body,
    bool IsHtml = true,
    List<string>? CcRecipients = null,
    List<string>? BccRecipients = null,
    DateTimeOffset CreatedAt = default
);
