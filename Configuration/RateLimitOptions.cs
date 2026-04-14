using System.ComponentModel.DataAnnotations;

namespace MSEMC.Configuration;

/// <summary>
/// Strongly-typed options for Rate Limiting configuration.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Maximum number of requests allowed in the time window.</summary>
    [Range(1, 10000)]
    public int PermitLimit { get; init; } = 100;

    /// <summary>Duration of the rate limiting window in seconds.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; init; } = 60;
}
