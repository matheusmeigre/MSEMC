using System.ComponentModel.DataAnnotations;

namespace MSEMC.Configuration;

/// <summary>
/// Configurações do sistema de templates — carregadas via Options Pattern.
/// Seção no appsettings.json: "Templates".
/// </summary>
public sealed class TemplateOptions
{
    public const string SectionName = "Templates";

    /// <summary>
    /// Caminho raiz absoluto (ou relativo ao ContentRootPath) onde os templates estão armazenados.
    /// Ex: "Templates" (relativo) ou "/app/Templates" (absoluto em container).
    /// </summary>
    [Required(ErrorMessage = "Templates:BasePath is required")]
    public required string BasePath { get; init; }

    /// <summary>
    /// Locale padrão utilizado como fallback quando o locale solicitado não possui o template.
    /// Corresponde à pasta "Templates/default/".
    /// </summary>
    public string DefaultLocale { get; init; } = "default";

    /// <summary>
    /// Tempo de retenção do cache de templates em memória (em segundos).
    /// Use 0 para desabilitar cache (útil em desenvolvimento com hot reload).
    /// </summary>
    [Range(0, int.MaxValue, ErrorMessage = "CacheExpirationSeconds must be >= 0")]
    public int CacheExpirationSeconds { get; init; } = 300;

    /// <summary>
    /// Tamanho máximo permitido por attachment em bytes. Padrão: 10 MB.
    /// </summary>
    [Range(1, long.MaxValue, ErrorMessage = "MaxAttachmentSizeBytes must be >= 1")]
    public long MaxAttachmentSizeBytes { get; init; } = 10 * 1024 * 1024;

    /// <summary>
    /// Número máximo de attachments permitidos por requisição de e-mail.
    /// </summary>
    [Range(1, 10, ErrorMessage = "MaxAttachmentsPerEmail must be between 1 and 10")]
    public int MaxAttachmentsPerEmail { get; init; } = 5;
}
