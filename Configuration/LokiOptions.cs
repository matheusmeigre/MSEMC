using System.ComponentModel.DataAnnotations;

namespace MSEMC.Configuration;

public sealed class LokiOptions
{
    public const string SectionName = "Loki";

    public bool Enabled { get; init; } = true;

    [Required]
    public string Uri { get; init; } = string.Empty;

    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    public string AppLabel { get; init; } = "msemc";

    public string EnvironmentLabel { get; init; } = "production";
}
