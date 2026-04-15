using MSEMC.Domain.Enums;

namespace MSEMC.Domain.Entities;

/// <summary>
/// Entidade de domínio rica que representa uma mensagem de e-mail ao longo do seu ciclo de vida.
/// Record imutável com transições de estado controladas via métodos dedicados.
/// </summary>
public sealed record EmailMessage
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Recipient { get; init; }

    public required string Subject { get; init; }

    public required string Body { get; init; }

    public bool IsHtml { get; init; } = true;

    public IReadOnlyList<string> CcRecipients { get; init; } = [];

    public IReadOnlyList<string> BccRecipients { get; init; } = [];

    public EmailStatus Status { get; private set; } = EmailStatus.Pending;

    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? SentAt { get; private set; }

    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Método factory — única forma de criar um EmailMessage válido.
    /// </summary>
    public static EmailMessage Create(
        string recipient,
        string subject,
        string body,
        bool isHtml = true,
        IReadOnlyList<string>? ccRecipients = null,
        IReadOnlyList<string>? bccRecipients = null) =>
        new()
        {
            Recipient = recipient ?? throw new ArgumentNullException(nameof(recipient)),
            Subject = subject ?? throw new ArgumentNullException(nameof(subject)),
            Body = body ?? throw new ArgumentNullException(nameof(body)),
            IsHtml = isHtml,
            CcRecipients = ccRecipients ?? [],
            BccRecipients = bccRecipients ?? []
        };

    /// <summary>Transiciona a mensagem para o status Sending.</summary>
    public void MarkAsSending()
    {
        if (Status is not (EmailStatus.Pending or EmailStatus.Queued or EmailStatus.Retrying))
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {EmailStatus.Sending}");

        Status = EmailStatus.Sending;
    }

    /// <summary>Transiciona a mensagem para o status Sent com registro de data/hora.</summary>
    public void MarkAsSent()
    {
        if (Status is not EmailStatus.Sending)
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {EmailStatus.Sent}");

        Status = EmailStatus.Sent;
        SentAt = DateTimeOffset.UtcNow;
        ErrorMessage = null;
    }

    /// <summary>Transiciona a mensagem para o status Failed com uma descrição do erro.</summary>
    public void MarkAsFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Status = EmailStatus.Failed;
        ErrorMessage = error;
    }

    /// <summary>Transiciona a mensagem para o status Queued (aceita para processamento assíncrono).</summary>
    public void MarkAsQueued()
    {
        if (Status is not EmailStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {EmailStatus.Queued}");

        Status = EmailStatus.Queued;
    }
}
