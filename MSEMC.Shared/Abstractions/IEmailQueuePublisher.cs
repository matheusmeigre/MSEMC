using MSEMC.Domain.Entities;

namespace MSEMC.Abstractions;

/// <summary>
/// Abstração para publicar mensagens de e-mail em uma fila para processamento assíncrono.
/// Desacopla a camada de API do broker de mensagens real (RabbitMQ, InMemory, etc.).
/// </summary>
public interface IEmailQueuePublisher
{
    /// <summary>
    /// Publica uma mensagem de e-mail na fila para entrega assíncrona.
    /// </summary>
    /// <param name="message">A mensagem de e-mail a ser enfileirada.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    Task PublishAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
