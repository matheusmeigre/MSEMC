namespace MSEMC.Messaging.Events;

/// <summary>
/// Domain event published when email delivery fails after all retry attempts.
/// </summary>
public sealed record EmailFailedEvent(
    Guid MessageId,
    string Recipient,
    string ErrorMessage,
    int RetryCount,
    DateTimeOffset FailedAt
);
