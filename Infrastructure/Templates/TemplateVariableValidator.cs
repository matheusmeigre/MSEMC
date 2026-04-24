using MSEMC.Domain.Entities;

namespace MSEMC.Infrastructure.Templates;

/// <summary>
/// Valida que todas as variáveis obrigatórias declaradas no .meta.json estão
/// presentes no payload de dados enviado pelo consumidor da API.
/// Implementa a estratégia "Fail Fast" — aborta antes de chamar a engine de renderização.
/// </summary>
public sealed class TemplateVariableValidator
{
    /// <summary>
    /// Valida se todos os campos obrigatórios do template estão presentes nos dados fornecidos.
    /// </summary>
    /// <param name="metadata">Metadados do template com lista de variáveis obrigatórias.</param>
    /// <param name="data">Dados fornecidos pelo consumidor da API.</param>
    /// <returns>Null se válido; string com lista de variáveis faltantes se inválido.</returns>
    public string? Validate(TemplateMetadata metadata, IDictionary<string, object?> data)
    {
        if (metadata.RequiredVariables.Count == 0)
            return null;

        var missing = metadata.RequiredVariables
            .Where(variable => !data.ContainsKey(variable) || data[variable] is null)
            .ToList();

        if (missing.Count == 0)
            return null;

        return $"Missing required template variables: {string.Join(", ", missing.Select(v => $"'{v}'"))}";
    }
}
