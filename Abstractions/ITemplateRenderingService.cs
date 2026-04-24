using System.Text.Json;
using MSEMC.Domain.Results;

namespace MSEMC.Abstractions;

/// <summary>
/// Orquestrador do pipeline completo de renderização de templates.
/// Executa: Load → Validate → Render (body + subject).
/// Ponto de entrada único para qualquer camada que precise renderizar um template.
/// </summary>
public interface ITemplateRenderingService
{
    /// <summary>
    /// Renderiza um template completo (HTML + subject) com os dados fornecidos.
    /// O pipeline falha rapidamente se: template não existe, variáveis obrigatórias
    /// faltam no payload, ou a engine retorna erro de sintaxe.
    /// </summary>
    /// <param name="templateId">Identificador do template (ex: "autenticacao/codigo-seguranca").</param>
    /// <param name="locale">Locale desejado (ex: "pt-BR"). Null usa fallback "default".</param>
    /// <param name="data">Dados dinâmicos como JsonElement (deserializado do request HTTP).</param>
    /// <param name="subjectOverride">Se fornecido, substitui o subjectTemplate do .meta.json.</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>Resultado com HTML renderizado, subject resolvido e locale efetivo.</returns>
    Task<Result<TemplateRenderResult>> RenderAsync(
        string templateId,
        string? locale,
        JsonElement data,
        string? subjectOverride = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Saída imutável do pipeline de renderização de um template.
/// </summary>
/// <param name="RenderedHtml">HTML final pronto para envio.</param>
/// <param name="ResolvedSubject">Assunto do e-mail renderizado (com variáveis preenchidas).</param>
/// <param name="ResolvedLocale">Locale efetivamente usado (pode diferir do solicitado se houve fallback).</param>
public sealed record TemplateRenderResult(
    string RenderedHtml,
    string ResolvedSubject,
    string ResolvedLocale);
