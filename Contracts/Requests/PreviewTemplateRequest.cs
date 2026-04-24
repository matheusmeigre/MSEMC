using System.Text.Json;

namespace MSEMC.Contracts.Requests;

/// <summary>
/// Contrato de requisição para pré-visualização de um template sem enviar e-mail.
/// Utilizado por painéis administrativos para preview antes de disparar campanhas.
/// </summary>
public sealed record PreviewTemplateRequest(
    /// <summary>Dados dinâmicos em camelCase para preencher as variáveis do template.</summary>
    JsonElement Data,
    /// <summary>Locale para seleção do template. Ex: "pt-BR". Null usa fallback "default".</summary>
    string? Locale = null,
    /// <summary>Subject override opcional. Se não fornecido, usa o subjectTemplate do .meta.json.</summary>
    string? SubjectOverride = null
);
