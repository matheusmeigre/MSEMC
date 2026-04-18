using System.ComponentModel.DataAnnotations;

namespace MSEMC.Configuration;

public sealed class BrevoOptions
{
    public const string SectionName = "Brevo";

    [Required(ErrorMessage = "Brevo ApiKey is required")]
    public required string ApiKey { get; init; }

    [Required(ErrorMessage = "Brevo SenderEmail is required")]
    [EmailAddress(ErrorMessage = "Brevo SenderEmail must be a valid email address")]
    public required string SenderEmail { get; init; }

    public string SenderDisplayName { get; init; } = "MSEMC";
}
