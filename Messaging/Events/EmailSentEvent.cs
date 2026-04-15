namespace MSEMC.Messaging.Events;

/// <summary>
/// Evento de domínio publicado quando um e-mail é entregue com sucesso.
/// </summary>
public sealed record EmailSentEvent(
    Guid MessageId,
    string Recipient,
    DateTimeOffset SentAt
);
