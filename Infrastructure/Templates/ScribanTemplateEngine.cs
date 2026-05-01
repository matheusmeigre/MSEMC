using Scriban;
using Scriban.Runtime;
using MSEMC.Abstractions;
using MSEMC.Domain.Results;
using System.Text.Json;

namespace MSEMC.Infrastructure.Templates;

/// <summary>
/// Implementação da engine de renderização usando Scriban.
/// Escolhido por: performance (AST parsing), zero dependências pesadas,
/// suporte nativo a Dictionary, listas e objetos aninhados, sandbox seguro.
/// Registrado como Singleton — Template.Parse é thread-safe.
/// </summary>
public sealed class ScribanTemplateEngine(
    ILogger<ScribanTemplateEngine> logger) : ITemplateEngine
{
    public async Task<Result<string>> RenderAsync(
        string templateContent,
        IDictionary<string, object?> data,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var template = Template.Parse(templateContent);

            if (template.HasErrors)
            {
                var errors = string.Join("; ", template.Messages.Select(m => m.Message));
                logger.LogError("Erro de sintaxe no template: {Errors}", errors);
                return Result<string>.Fail($"Template syntax error: {errors}");
            }

            var scriptObject = BuildScriptObject(data);
            logger.LogInformation("Renderizando template com as chaves: {Keys}", string.Join(", ", data.Keys));
            
            var templateContext = new TemplateContext();
            templateContext.PushGlobal(scriptObject);

            // Scriban é síncrono por natureza; envolvemos em Task para respeitar o contrato async
            var rendered = await Task.Run(() => template.Render(templateContext), cancellationToken);

            return Result<string>.Ok(rendered);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao renderizar template Scriban");
            return Result<string>.Fail($"Template rendering failed: {ex.Message}");
        }
    }

    public IReadOnlySet<string> ExtractVariables(string templateContent)
    {
        var template = Template.Parse(templateContent);

        if (template.HasErrors)
            return new HashSet<string>();

        var visitor = new VariableExtractorVisitor();
        visitor.Visit(template.Page);
        return visitor.Variables;
    }

    /// <summary>
    /// Converte um Dictionary<string, object?> em um ScriptObject do Scriban.
    /// Processa recursivamente JsonElement e outros tipos para garantir que o Scriban consiga ler os dados.
    /// </summary>
    private static ScriptObject BuildScriptObject(IDictionary<string, object?> data)
    {
        var scriptObject = new ScriptObject();
        // Usamos Import com o dicionário normalizado para permitir que o Scriban
        // lide com o mapeamento de nomes (case-insensitivity via snake_case interno).
        scriptObject.Import(NormalizeDictionary(data));
        return scriptObject;
    }

    private static IDictionary<string, object?> NormalizeDictionary(IDictionary<string, object?> data)
    {
        return data.ToDictionary(kvp => kvp.Key, kvp => NormalizeValue(kvp.Value));
    }

    private static object? NormalizeValue(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => element.EnumerateArray().Select(NormalizeValue).ToList(),
                JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => NormalizeValue(p.Value)),
                _ => element.GetRawText()
            };
        }

        if (value is IDictionary<string, object?> dict)
        {
            return NormalizeDictionary(dict);
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var list = new List<object?>();
            foreach (var item in enumerable)
            {
                list.Add(NormalizeValue(item));
            }
            return list;
        }

        return value;
    }

    /// <summary>
    /// Visitor que percorre o AST do Scriban para extrair nomes de variáveis.
    /// </summary>
    private sealed class VariableExtractorVisitor : Scriban.Syntax.ScriptVisitor
    {
        public HashSet<string> Variables { get; } = new(StringComparer.OrdinalIgnoreCase);

        public override void Visit(Scriban.Syntax.ScriptVariableGlobal node)
        {
            Variables.Add(node.Name);
            base.Visit(node);
        }
    }
}
