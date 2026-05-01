using MassTransit;
using MSEMC.Abstractions;
using MSEMC.Domain.Entities;
using MSEMC.Messaging.Commands;
using MSEMC.Messaging.Events;

namespace MSEMC.Messaging.Consumers;

/// <summary>
/// Consumer MassTransit responsável por renderizar o template e enviar o resumo (digest) de IA (LLM).
/// Delega a renderização ao ITemplateRenderingService e o envio ao IEmailSender.
/// </summary>
public sealed class SendLlmDigestConsumer(
    ITemplateRenderingService templateRenderingService,
    IEmailSender emailSender,
    ILogger<SendLlmDigestConsumer> logger) : IConsumer<SendLlmDigestCommand>
{
    public async Task Consume(ConsumeContext<SendLlmDigestCommand> context)
    {
        var cmd = context.Message;

        logger.LogInformation(
            "Processando LLM Digest para {Recipient} usando template {TemplateId} (MessageId: {MessageId})",
            cmd.Recipient, cmd.TemplateId, cmd.MessageId);

        // 1. Renderiza o template
        var renderResult = await templateRenderingService.RenderAsync(
            templateId: cmd.TemplateId,
            locale: cmd.Locale,
            data: cmd.Data,
            cancellationToken: context.CancellationToken);

        if (!renderResult.IsSuccess)
        {
            logger.LogError(
                "Falha ao renderizar template {TemplateId} para {Recipient} (MessageId: {MessageId}): {Error}",
                cmd.TemplateId, cmd.Recipient, cmd.MessageId, renderResult.Error);
                
            throw new InvalidOperationException($"Falha de renderização do template {cmd.TemplateId}: {renderResult.Error}");
        }

        // 2. Prepara a mensagem de e-mail usando o HTML e Assunto renderizados
        var message = EmailMessage.Create(
            recipient: cmd.Recipient,
            subject: renderResult.Value!.ResolvedSubject,
            body: renderResult.Value!.RenderedHtml,
            isHtml: true);

        // 3. Envia o e-mail via IEmailSender
        var sendResult = await emailSender.SendAsync(message, context.CancellationToken);

        if (sendResult.IsSuccess)
        {
            logger.LogInformation(
                "LLM Digest entregue para {Recipient} (MessageId: {MessageId})",
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
                "Falha na entrega do LLM Digest para {Recipient} (MessageId: {MessageId}): {Error}",
                cmd.Recipient, cmd.MessageId, sendResult.Error);

            await context.Publish(new EmailFailedEvent(
                MessageId: cmd.MessageId,
                Recipient: cmd.Recipient,
                ErrorMessage: sendResult.Error ?? "Unknown error",
                RetryCount: 0,
                FailedAt: DateTimeOffset.UtcNow),
                context.CancellationToken);

            // Re-throw para garantir o retry policy nativo do MassTransit (se configurado)
            throw new InvalidOperationException($"Falha ao enviar LLM Digest: {sendResult.Error}");
        }
    }
}
