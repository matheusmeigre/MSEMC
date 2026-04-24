namespace MSEMC.Domain.Entities;

/// <summary>
/// Representação fortemente tipada dos metadados de um template (.meta.json).
/// Carregada pelo ITemplateLoader e usada pelo pipeline de validação para
/// garantir que todos os dados obrigatórios estejam presentes antes de renderizar.
/// </summary>
public sealed record TemplateMetadata
{
    /// <summary>Nome legível do template (ex: "Código de Segurança").</summary>
    public required string Name { get; init; }

    /// <summary>Descrição do propósito e cenário de uso do template.</summary>
    public required string Description { get; init; }

    /// <summary>Domínio de negócio ao qual pertence (ex: "autenticacao", "financeiro").</summary>
    public required string Domain { get; init; }

    /// <summary>
    /// Template do assunto do e-mail com suporte a variáveis Scriban.
    /// Ex: "Seu código de segurança: {{ codigoSeguranca }}"
    /// Null indica que o subject deve ser fornecido pelo chamador via subjectOverride.
    /// </summary>
    public string? SubjectTemplate { get; init; }

    /// <summary>
    /// Variáveis obrigatórias que DEVEM estar presentes no payload.
    /// Se alguma estiver ausente, o pipeline retorna erro 400 antes de renderizar.
    /// Nomes em camelCase (ex: ["nomeUsuario", "codigoSeguranca"]).
    /// </summary>
    public IReadOnlyList<string> RequiredVariables { get; init; } = [];

    /// <summary>
    /// Variáveis opcionais que podem estar ausentes sem bloquear a renderização.
    /// O template deve lidar com valores nulos (ex: {{ if nomeEmpresa }} ... {{ end }}).
    /// </summary>
    public IReadOnlyList<string> OptionalVariables { get; init; } = [];
}
