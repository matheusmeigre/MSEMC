namespace MSEMC.Contracts.Responses;

/// <summary>
/// API contract returned after a message is accepted for delivery.
/// </summary>
public sealed record SendEmailResponse(
    Guid MessageId,
    string Status,
    DateTimeOffset AcceptedAt
);
