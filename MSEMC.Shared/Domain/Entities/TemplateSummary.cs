namespace MSEMC.Domain.Entities;

/// <summary>
/// Resumo de um template disponível no catálogo.
/// Retornado pelo endpoint GET /api/templates para discovery de frontend.
/// </summary>
public sealed record TemplateSummary
{
    /// <summary>Identificador único do template (ex: "autenticacao/codigo-seguranca").</summary>
    public required string TemplateId { get; init; }

    /// <summary>Nome legível do template.</summary>
    public required string Name { get; init; }

    /// <summary>Descrição do caso de uso do template.</summary>
    public required string Description { get; init; }

    /// <summary>Domínio ao qual pertence (ex: "autenticacao", "financeiro").</summary>
    public required string Domain { get; init; }

    /// <summary>Template de assunto com variáveis Scriban (ex: "Seu código: {{ codigoSeguranca }}").</summary>
    public string? SubjectTemplate { get; init; }

    /// <summary>Variáveis que DEVEM estar presentes no payload data.</summary>
    public IReadOnlyList<string> RequiredVariables { get; init; } = [];

    /// <summary>Variáveis que podem estar presentes mas não são obrigatórias.</summary>
    public IReadOnlyList<string> OptionalVariables { get; init; } = [];

    /// <summary>Exemplo de payload JSON pronto para colar no Swagger/preview.</summary>
    public Dictionary<string, object?> ExamplePayload { get; init; } = [];
}
