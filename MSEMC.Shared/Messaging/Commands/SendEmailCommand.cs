namespace MSEMC.Messaging.Commands;

/// <summary>
/// Comando MassTransit para envio assíncrono de e-mail via fila de mensagens.
/// Publicado pela API e consumido pelo SendEmailConsumer.
/// </summary>
public sealed record SendEmailCommand(
    Guid MessageId,
    string Recipient,
    string Subject,
    string Body,
    bool IsHtml = true,
    List<string>? CcRecipients = null,
    List<string>? BccRecipients = null,
    List<MSEMC.Domain.Entities.EmailAttachment>? Attachments = null,
    DateTimeOffset CreatedAt = default
);
