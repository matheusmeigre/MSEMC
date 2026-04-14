namespace MSEMC.Messaging.Events;

/// <summary>
/// Domain event published when an email is successfully delivered.
/// </summary>
public sealed record EmailSentEvent(
    Guid MessageId,
    string Recipient,
    DateTimeOffset SentAt
);
