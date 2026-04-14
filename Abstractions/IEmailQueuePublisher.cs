using MSEMC.Domain.Entities;

namespace MSEMC.Abstractions;

/// <summary>
/// Abstraction for publishing email messages to a message queue for async processing.
/// Decouples the API layer from the actual message broker (RabbitMQ, InMemory, etc.).
/// </summary>
public interface IEmailQueuePublisher
{
    /// <summary>
    /// Publishes an email message to the queue for asynchronous delivery.
    /// </summary>
    /// <param name="message">The email message to enqueue.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    Task PublishAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
