using Scriban;
using Scriban.Runtime;
using MSEMC.Abstractions;
using MSEMC.Domain.Results;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MSEMC.Infrastructure.Templates;

/// <summary>
/// Implementação da engine de renderização usando Scriban.
/// Escolhido por: performance (AST parsing), zero dependências pesadas,
/// suporte nativo a Dictionary, listas e objetos aninhados, sandbox seguro.
/// Registrado como Singleton — Template.Parse é thread-safe.
/// </summary>
public sealed partial class ScribanTemplateEngine(
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
            
            // Log detalhado para debug: mostra chaves e tipos de valor reais
            var debugEntries = data.Select(kvp => $"{kvp.Key}={kvp.Value?.GetType().Name ?? "null"}").ToList();
            logger.LogInformation("Renderizando template com as chaves: {Keys} | Tipos: {Types}",
                string.Join(", ", data.Keys), string.Join(", ", debugEntries));
            
            var templateContext = new TemplateContext();
            // Digest emails com centenas de modelos geram ~5 loops aninhados:
            // provider_groups → models → (badges + changes + metrics).
            // O limite padrão de 1000 é insuficiente para payloads grandes.
            templateContext.LoopLimit = 10_000;
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
    /// Converte um Dictionary em um ScriptObject do Scriban.
    /// Registra cada chave em MÚLTIPLOS formatos (original, snake_case, lowercase)
    /// para garantir que o template encontre a variável independente do casing usado.
    /// Isso é necessário porque:
    ///   - O JSON do RabbitMQ usa camelCase (referenceDate)
    ///   - O template Scriban pode usar PascalCase (ReferenceDate)
    ///   - O Scriban internamente resolve variáveis com match EXATO no ScriptObject[]
    /// </summary>
    private static ScriptObject BuildScriptObject(IDictionary<string, object?> data)
    {
        var scriptObject = new ScriptObject();
        
        foreach (var (key, value) in data)
        {
            var normalizedValue = NormalizeValue(value);
            
            // Registra no formato original (ex: "referenceDate")
            scriptObject[key] = normalizedValue;
            
            // Registra em snake_case (ex: "reference_date") — o Scriban converte
            // PascalCase do template para snake_case ao fazer lookup
            var snakeKey = ToSnakeCase(key);
            if (snakeKey != key)
                scriptObject[snakeKey] = normalizedValue;
            
            // Registra em lowercase (ex: "referencedate") — fallback extra
            var lowerKey = key.ToLowerInvariant();
            if (lowerKey != key && lowerKey != snakeKey)
                scriptObject[lowerKey] = normalizedValue;
        }

        return scriptObject;
    }

    /// <summary>
    /// Converte camelCase ou PascalCase para snake_case.
    /// Ex: "referenceDate" → "reference_date", "Changes" → "changes"
    /// </summary>
    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return SnakeCaseRegex().Replace(input, "_$1").ToLowerInvariant().TrimStart('_');
    }

    [GeneratedRegex("([A-Z])")]
    private static partial Regex SnakeCaseRegex();

    /// <summary>
    /// Normaliza valores recursivamente. Converte JsonElement (que vem do MassTransit/System.Text.Json)
    /// em tipos primitivos do C# que o Scriban consegue processar nativamente.
    /// </summary>
    private static object? NormalizeValue(object? value)
    {
        if (value is JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.TryGetInt64(out var l) ? (object)l : element.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array => NormalizeArray(element),
                JsonValueKind.Object => NormalizeObject(element),
                _ => element.GetRawText()
            };
        }

        if (value is IDictionary<string, object?> dict)
        {
            return BuildScriptObject(dict);
        }

        if (value is System.Collections.IEnumerable enumerable && value is not string)
        {
            var array = new ScriptArray();
            foreach (var item in enumerable)
            {
                array.Add(NormalizeValue(item));
            }
            return array;
        }

        return value;
    }

    /// <summary>
    /// Converte um JsonElement array em um ScriptArray do Scriban.
    /// </summary>
    private static ScriptArray NormalizeArray(JsonElement arrayElement)
    {
        var array = new ScriptArray();
        foreach (var item in arrayElement.EnumerateArray())
        {
            array.Add(NormalizeValue(item));
        }
        return array;
    }

    /// <summary>
    /// Converte um JsonElement object em um ScriptObject do Scriban,
    /// registrando cada propriedade em múltiplos formatos de casing.
    /// </summary>
    private static ScriptObject NormalizeObject(JsonElement objectElement)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in objectElement.EnumerateObject())
        {
            dict[prop.Name] = prop.Value;
        }
        return BuildScriptObject(dict);
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
