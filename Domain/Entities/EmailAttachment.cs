namespace MSEMC.Domain.Entities;

/// <summary>
/// Value object imutável que representa um anexo de e-mail codificado em Base64.
/// A responsabilidade de geração do conteúdo (PDF, DOCX etc.) é do serviço de origem,
/// não do MSEMC. Esta entidade apenas transporta o binário pré-gerado.
/// </summary>
public sealed record EmailAttachment
{
    /// <summary>Nome do arquivo exibido no cliente de e-mail (ex: "fatura-abril-2026.pdf").</summary>
    public required string FileName { get; init; }

    /// <summary>Media type do conteúdo (ex: "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document").</summary>
    public required string ContentType { get; init; }

    /// <summary>Conteúdo do arquivo codificado em Base64.</summary>
    public required string ContentBase64 { get; init; }

    /// <summary>
    /// Decodifica o ContentBase64 em array de bytes para uso pelos email senders.
    /// </summary>
    /// <returns>Bytes do arquivo pronto para anexação.</returns>
    public byte[] GetContentBytes() => Convert.FromBase64String(ContentBase64);

    /// <summary>
    /// Calcula o tamanho aproximado do arquivo em bytes (a partir do Base64).
    /// </summary>
    public long GetApproximateSizeBytes() => (long)(ContentBase64.Length * 0.75);
}
