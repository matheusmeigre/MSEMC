using System.ComponentModel.DataAnnotations;

namespace MSEMC.Configuration;

/// <summary>
/// Opções fortemente tipadas para configuração SMTP.
/// Validadas na inicialização via DataAnnotations.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    [Required(ErrorMessage = "SMTP Host is required")]
    public required string Host { get; init; }

    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public required int Port { get; init; }

    [Required(ErrorMessage = "SMTP Username is required")]
    public required string Username { get; init; }

    [Required(ErrorMessage = "SMTP Password is required")]
    public required string Password { get; init; }

    public bool EnableSsl { get; init; } = true;

    [Required(ErrorMessage = "Sender email is required")]
    [EmailAddress(ErrorMessage = "Sender email must be a valid email address")]
    public required string SenderEmail { get; init; }

    public string SenderDisplayName { get; init; } = "MSEMC";
}
