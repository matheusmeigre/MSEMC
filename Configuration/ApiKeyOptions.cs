using System.ComponentModel.DataAnnotations;

namespace MSEMC.Configuration;

/// <summary>
/// Strongly-typed options for API Key authentication.
/// </summary>
public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    /// <summary>HTTP header name carrying the API key.</summary>
    public string HeaderName { get; init; } = "X-API-Key";

    /// <summary>The expected API key value. Must be kept secret.</summary>
    [Required(ErrorMessage = "API Key is required for production")]
    public required string Key { get; init; }
}
