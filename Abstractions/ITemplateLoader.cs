using MSEMC.Domain.Entities;
using MSEMC.Domain.Results;

namespace MSEMC.Abstractions;

/// <summary>
/// Abstração para carregamento de templates do armazenamento físico.
/// Suporta resolução de locale com fallback e cache em memória.
/// Implementação atual: sistema de arquivos. Pode ser trocada por Blob Storage
/// sem alterar a camada de aplicação (OCP).
/// </summary>
public interface ITemplateLoader
{
    /// <summary>
    /// Carrega o conteúdo HTML de um template pelo ID e locale.
    /// Aplica fallback para locale "default" se o locale específico não existir.
    /// Resolução de path: Templates/{locale}/{templateId}.html → Templates/default/{templateId}.html
    /// </summary>
    /// <param name="templateId">Identificador do template (ex: "autenticacao/codigo-seguranca").</param>
    /// <param name="locale">Locale desejado (ex: "pt-BR"). Null usa o locale padrão configurado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Conteúdo HTML do template ou erro descritivo.</returns>
    Task<Result<string>> LoadContentAsync(
        string templateId,
        string? locale = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Carrega os metadados de um template a partir do arquivo .meta.json adjacente.
    /// Contém variáveis obrigatórias/opcionais e subjectTemplate para renderização do assunto.
    /// </summary>
    /// <param name="templateId">Identificador do template.</param>
    /// <param name="locale">Locale desejado. Null usa o locale padrão configurado.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Metadados fortemente tipados do template ou erro.</returns>
    Task<Result<TemplateMetadata>> LoadMetadataAsync(
        string templateId,
        string? locale = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lista todos os templates disponíveis no armazenamento para um dado locale,
    /// retornando seus metadados completos (id, nome, domínio, variáveis).
    /// Ignora arquivos de layout (_layouts/) e qualquer arquivo corrompido.
    /// </summary>
    /// <param name="locale">Locale para resolução. Null usa o locale padrão configurado.</param>
    /// <param name="domain">Filtro opcional por domínio (ex: "autenticacao", "financeiro").</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Lista de <see cref="TemplateSummary"/> ou resultado de erro.</returns>
    Task<Result<IReadOnlyList<TemplateSummary>>> ListAsync(
        string? locale = null,
        string? domain = null,
        CancellationToken cancellationToken = default);
}
