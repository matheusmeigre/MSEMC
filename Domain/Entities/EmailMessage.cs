using MSEMC.Domain.Enums;

namespace MSEMC.Domain.Entities;

/// <summary>
/// Rich domain entity representing an email message throughout its lifecycle.
/// Immutable record with controlled state transitions via dedicated methods.
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
    /// Factory method — the only way to create a valid EmailMessage.
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

    /// <summary>Transitions the message to Sending status.</summary>
    public void MarkAsSending()
    {
        if (Status is not (EmailStatus.Pending or EmailStatus.Queued or EmailStatus.Retrying))
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {EmailStatus.Sending}");

        Status = EmailStatus.Sending;
    }

    /// <summary>Transitions the message to Sent status with a timestamp.</summary>
    public void MarkAsSent()
    {
        if (Status is not EmailStatus.Sending)
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {EmailStatus.Sent}");

        Status = EmailStatus.Sent;
        SentAt = DateTimeOffset.UtcNow;
        ErrorMessage = null;
    }

    /// <summary>Transitions the message to Failed status with an error description.</summary>
    public void MarkAsFailed(string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        Status = EmailStatus.Failed;
        ErrorMessage = error;
    }

    /// <summary>Transitions the message to Queued status (accepted for async processing).</summary>
    public void MarkAsQueued()
    {
        if (Status is not EmailStatus.Pending)
            throw new InvalidOperationException(
                $"Cannot transition from {Status} to {EmailStatus.Queued}");

        Status = EmailStatus.Queued;
    }
}
