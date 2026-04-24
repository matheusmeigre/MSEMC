using Scriban;
using Scriban.Runtime;
using MSEMC.Abstractions;
using MSEMC.Domain.Results;

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
            var templateContext = new TemplateContext
            {
                // Mantém camelCase — nomes das variáveis como chegam do JSON
                MemberRenamer = member => member.Name,
                // Strict mode: não silencia variáveis não resolvidas (fail-fast)
                StrictVariables = false
            };
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
    /// Processa recursivamente nested objects (ScriptObject) e arrays (ScriptArray).
    /// </summary>
    private static ScriptObject BuildScriptObject(IDictionary<string, object?> data)
    {
        var scriptObject = new ScriptObject();

        foreach (var (key, value) in data)
        {
            scriptObject[key] = ConvertValue(value);
        }

        return scriptObject;
    }

    private static object? ConvertValue(object? value)
    {
        return value switch
        {
            null => null,
            string s => s,
            bool b => b,
            int or long or double or float or decimal => value,
            IDictionary<string, object?> dict => BuildScriptObject(dict),
            System.Collections.IEnumerable enumerable => ConvertArray(enumerable),
            _ => value
        };
    }

    private static ScriptArray ConvertArray(System.Collections.IEnumerable enumerable)
    {
        var array = new ScriptArray();
        foreach (var item in enumerable)
        {
            array.Add(item is IDictionary<string, object?> dict
                ? BuildScriptObject(dict)
                : ConvertValue(item));
        }
        return array;
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
