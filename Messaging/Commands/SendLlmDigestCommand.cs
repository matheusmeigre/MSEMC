using System.Text.Json;

namespace MSEMC.Messaging.Commands;

/// <summary>
/// Comando MassTransit para processar e enviar o resumo (digest) gerado por IA (LLM).
/// Consumido pelo SendLlmDigestConsumer.
/// </summary>
public sealed record SendLlmDigestCommand(
    Guid MessageId,
    string Recipient,
    string TemplateId,
    JsonElement Data,
    string? Locale = "pt-BR",
    DateTimeOffset CreatedAt = default
);
