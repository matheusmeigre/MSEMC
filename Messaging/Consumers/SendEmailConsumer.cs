using MassTransit;
using MSEMC.Abstractions;
using MSEMC.Domain.Entities;
using MSEMC.Messaging.Commands;
using MSEMC.Messaging.Events;

namespace MSEMC.Messaging.Consumers;

/// <summary>
/// Consumer MassTransit que processa mensagens SendEmailCommand da fila.
/// Delega o envio real ao IEmailSender e publica eventos de resultado.
/// </summary>
public sealed class SendEmailConsumer(
    IEmailSender emailSender,
    ILogger<SendEmailConsumer> logger) : IConsumer<SendEmailCommand>
{
    public async Task Consume(ConsumeContext<SendEmailCommand> context)
    {
        var cmd = context.Message;

        logger.LogInformation(
            "Processando comando de e-mail para {Recipient} (MessageId: {MessageId})",
            cmd.Recipient, cmd.MessageId);

        var message = EmailMessage.Create(
            recipient: cmd.Recipient,
            subject: cmd.Subject,
            body: cmd.Body,
            isHtml: cmd.IsHtml,
            ccRecipients: cmd.CcRecipients,
            bccRecipients: cmd.BccRecipients,
            attachments: cmd.Attachments);

        var result = await emailSender.SendAsync(message, context.CancellationToken);

        if (result.IsSuccess)
        {
            logger.LogInformation(
                "E-mail entregue para {Recipient} (MessageId: {MessageId})",
                cmd.Recipient, cmd.MessageId);

            await context.Publish(new EmailSentEvent(
                MessageId: cmd.MessageId,
                Recipient: cmd.Recipient,
                SentAt: DateTimeOffset.UtcNow),
                context.CancellationToken);
        }
        else
        {
            logger.LogWarning(
                "Falha na entrega de e-mail para {Recipient} (MessageId: {MessageId}): {Error}",
                cmd.Recipient, cmd.MessageId, result.Error);

            await context.Publish(new EmailFailedEvent(
                MessageId: cmd.MessageId,
                Recipient: cmd.Recipient,
                ErrorMessage: result.Error ?? "Unknown error",
                RetryCount: 0,
                FailedAt: DateTimeOffset.UtcNow),
                context.CancellationToken);
        }
    }
}
