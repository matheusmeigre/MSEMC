using MSEMC.Domain.Results;

namespace MSEMC.Abstractions;

/// <summary>
/// Abstração do motor de renderização de templates.
/// Encapsula a engine concreta (Scriban) para inversão de dependência (DIP).
/// </summary>
public interface ITemplateEngine
{
    /// <summary>
    /// Renderiza um template com os dados fornecidos.
    /// </summary>
    /// <param name="templateContent">Conteúdo HTML bruto do template com variáveis {{ camelCase }}.</param>
    /// <param name="data">Dados dinâmicos para preencher as variáveis (chaves em camelCase).</param>
    /// <param name="cancellationToken">Token de cancelamento.</param>
    /// <returns>HTML renderizado em caso de sucesso, ou mensagem de erro.</returns>
    Task<Result<string>> RenderAsync(
        string templateContent,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extrai os nomes de todas as variáveis declaradas em um template ({{ variavel }}).
    /// Usado para validação pré-renderização (fail-fast).
    /// </summary>
    /// <param name="templateContent">Conteúdo HTML bruto do template.</param>
    /// <returns>Conjunto imutável de nomes de variáveis encontradas no template.</returns>
    IReadOnlySet<string> ExtractVariables(string templateContent);
}
