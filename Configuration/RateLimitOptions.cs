using System.ComponentModel.DataAnnotations;

namespace MSEMC.Configuration;

/// <summary>
/// Opções fortemente tipadas para configuração de Rate Limiting.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Número máximo de requisições permitidas na janela de tempo.</summary>
    [Range(1, 10000)]
    public int PermitLimit { get; init; } = 100;

    /// <summary>Duração da janela de rate limiting em segundos.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;
}
