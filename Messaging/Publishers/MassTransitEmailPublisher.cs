using MassTransit;
using MSEMC.Abstractions;
using MSEMC.Domain.Entities;
using MSEMC.Messaging.Commands;

namespace MSEMC.Messaging.Publishers;

/// <summary>
/// Publica mensagens de e-mail na fila via MassTransit.
/// Implementa <see cref="IEmailQueuePublisher"/> para inversão de dependência.
/// </summary>
public sealed class MassTransitEmailPublisher(
    IPublishEndpoint publishEndpoint,
    ILogger<MassTransitEmailPublisher> logger) : IEmailQueuePublisher
{
    public async Task PublishAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Publicando comando de e-mail na fila para {Recipient} (MessageId: {MessageId})",
            message.Recipient, message.Id);

        var command = new SendEmailCommand(
            MessageId: message.Id,
            Recipient: message.Recipient,
            Subject: message.Subject,
            Body: message.Body,
            IsHtml: message.IsHtml,
            CcRecipients: message.CcRecipients.ToList(),
            BccRecipients: message.BccRecipients.ToList(),
            CreatedAt: message.CreatedAt);

        await publishEndpoint.Publish(command, cancellationToken);

        message.MarkAsQueued();

        logger.LogInformation(
            "Comando de e-mail publicado para {Recipient} (MessageId: {MessageId})",
            message.Recipient, message.Id);
    }
}
