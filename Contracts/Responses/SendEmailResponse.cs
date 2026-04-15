namespace MSEMC.Contracts.Responses;

/// <summary>
/// Contrato de resposta retornado após uma mensagem ser aceita para entrega.
/// </summary>
public sealed record SendEmailResponse(
    Guid MessageId,
    string Status,
    DateTimeOffset AcceptedAt
);
