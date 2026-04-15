using System.ComponentModel.DataAnnotations;

namespace MSEMC.Configuration;

/// <summary>
/// Opções fortemente tipadas para autenticação via API Key.
/// </summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    /// <summary>Nome do header HTTP que transporta a API Key.</summary>
    public string HeaderName { get; init; } = "X-API-Key";

    /// <summary>Valor esperado da API Key. Deve ser mantido em segredo.</summary>
    [Required(ErrorMessage = "API Key is required for production")]
    public required string Key { get; init; }
}
