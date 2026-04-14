using System.ComponentModel.DataAnnotations;

namespace MSEMC.Models;

/// <summary>
/// Legacy DTO — will be removed in Phase 4.
/// Replaced by <see cref="Contracts.Requests.SendEmailRequest"/>.
/// </summary>
[Obsolete("Use Contracts.Requests.SendEmailRequest instead. Will be removed in Phase 4.")]
public class RequisicaoEmail
{
    [Required]
    [EmailAddress]
    public required string Destinatario { get; set; }

    [Required]
    public required string Assunto { get; set; }

    [Required]
    public required string Conteudo { get; set; }

    public required List<string> Destinatarios { get; set; }
    public List<string>? Cc { get; set; }
    public List<string>? Bcc { get; set; }

    public List<object>? Attachments { get; set; }
}
