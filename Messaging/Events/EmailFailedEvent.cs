namespace MSEMC.Messaging.Events;

/// <summary>
/// Evento de domínio publicado quando a entrega do e-mail falha após todas as tentativas de retry.
/// </summary>
public sealed record EmailFailedEvent(
    Guid MessageId,
    string Recipient,
    string ErrorMessage,
    int RetryCount,
    DateTimeOffset FailedAt
);
