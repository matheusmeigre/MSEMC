namespace MSEMC.Domain.Enums;

/// <summary>
/// Represents the lifecycle status of an email message.
/// </summary>
public enum EmailStatus
{
    /// <summary>Message created but not yet queued for delivery.</summary>
    Pending = 0,

    /// <summary>Message accepted and queued for asynchronous processing.</summary>
    Queued = 1,

    /// <summary>Message is currently being sent via SMTP.</summary>
    Sending = 2,

    /// <summary>Message was successfully delivered to the SMTP server.</summary>
    Sent = 3,

    /// <summary>Message delivery failed after all retry attempts.</summary>
    Failed = 4,

    /// <summary>Message delivery failed and is being retried.</summary>
    Retrying = 5
}
