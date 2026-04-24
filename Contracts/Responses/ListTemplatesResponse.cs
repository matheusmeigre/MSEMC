namespace MSEMC.Contracts.Responses;

/// <summary>
/// Resposta do catálogo GET /api/templates.
/// Contém todos os templates disponíveis com metadados e exemplo de payload.
/// </summary>
public sealed record ListTemplatesResponse(
    IReadOnlyList<TemplateEntry> Templates,
    int Total
);

/// <summary>
/// Entrada individual no catálogo de templates.
/// </summary>
public sealed record TemplateEntry(
    string TemplateId,
    string Name,
    string Description,
    string Domain,
    string? SubjectTemplate,
    IReadOnlyList<string> RequiredVariables,
    IReadOnlyList<string> OptionalVariables,
    Dictionary<string, object?> ExamplePayload,
    string PreviewEndpoint
);
