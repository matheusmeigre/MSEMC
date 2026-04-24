using System.Text.Json;
using MSEMC.Abstractions;
using MSEMC.Infrastructure.Templates;
using MSEMC.Domain.Results;

namespace MSEMC.Services;

/// <summary>
/// Orquestrador do pipeline de renderização de templates:
/// Load Content → Load Metadata → Validate Variables → Render Body → Render Subject.
/// Registrado como Scoped — recebe ITemplateLoader e ITemplateEngine Singleton com segurança.
/// </summary>
public sealed class TemplateRenderingService(
    ITemplateLoader loader,
    ITemplateEngine engine,
    TemplateVariableValidator validator,
    ILogger<TemplateRenderingService> logger) : ITemplateRenderingService
{
    public async Task<Result<TemplateRenderResult>> RenderAsync(
        string templateId,
        string? locale,
        JsonElement data,
        string? subjectOverride = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Iniciando renderização do template '{TemplateId}' para locale '{Locale}'",
            templateId, locale ?? "default");

        // ── STEP 1: Carregar conteúdo do template ──────────────────────────────────
        var contentResult = await loader.LoadContentAsync(templateId, locale, cancellationToken);
        if (!contentResult.IsSuccess)
        {
            logger.LogWarning("Template '{TemplateId}' não encontrado: {Error}", templateId, contentResult.Error);
            return Result<TemplateRenderResult>.Fail(contentResult.Error!);
        }

        // ── STEP 2: Carregar metadados do template ──────────────────────────────────
        var metadataResult = await loader.LoadMetadataAsync(templateId, locale, cancellationToken);
        if (!metadataResult.IsSuccess)
        {
            logger.LogWarning("Metadados do template '{TemplateId}' não encontrados: {Error}", templateId, metadataResult.Error);
            return Result<TemplateRenderResult>.Fail(metadataResult.Error!);
        }

        var metadata = metadataResult.Value!;

        // ── STEP 3: Converter JsonElement → Dictionary (recursivo) ─────────────────
        var dataDict = ConvertJsonElement(data);

        // ── STEP 4: Validar variáveis obrigatórias (Fail-Fast) ─────────────────────
        var validationError = validator.Validate(metadata, dataDict);
        if (validationError is not null)
        {
            logger.LogWarning(
                "Validação falhou para template '{TemplateId}': {Error}",
                templateId, validationError);
            return Result<TemplateRenderResult>.Fail(validationError);
        }

        // ── STEP 5: Renderizar o corpo HTML ────────────────────────────────────────
        var bodyResult = await engine.RenderAsync(contentResult.Value!, dataDict, cancellationToken);
        if (!bodyResult.IsSuccess)
        {
            logger.LogError("Falha na renderização do body do template '{TemplateId}': {Error}", templateId, bodyResult.Error);
            return Result<TemplateRenderResult>.Fail(bodyResult.Error!);
        }

        // ── STEP 6: Renderizar o subject ───────────────────────────────────────────
        string resolvedSubject;
        if (subjectOverride is { Length: > 0 })
        {
            resolvedSubject = subjectOverride;
        }
        else if (metadata.SubjectTemplate is { Length: > 0 })
        {
            var subjectResult = await engine.RenderAsync(metadata.SubjectTemplate, dataDict, cancellationToken);
            resolvedSubject = subjectResult.IsSuccess
                ? subjectResult.Value!.Trim()
                : metadata.Name; // Fallback para o nome do template se subject falhar
        }
        else
        {
            resolvedSubject = metadata.Name;
        }

        // Resolve qual locale foi efetivamente usado (pode haver fallback para "default")
        var resolvedLocale = locale ?? "default";

        logger.LogInformation(
            "Template '{TemplateId}' renderizado com sucesso. Subject: '{Subject}'",
            templateId, resolvedSubject);

        return Result<TemplateRenderResult>.Ok(new TemplateRenderResult(
            RenderedHtml: bodyResult.Value!,
            ResolvedSubject: resolvedSubject,
            ResolvedLocale: resolvedLocale));
    }

    /// <summary>
    /// Converte recursivamente um JsonElement para IDictionary&lt;string, object?&gt;.
    /// Suporta: objetos aninhados, arrays, strings, números, booleans, null.
    /// </summary>
    private static Dictionary<string, object?> ConvertJsonElement(JsonElement element)
    {
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (element.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var property in element.EnumerateObject())
        {
            result[property.Name] = ConvertJsonValue(property.Value);
        }

        return result;
    }

    private static object? ConvertJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => ConvertJsonElement(element),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonValue)
                .ToList(),
            _ => element.GetRawText()
        };
    }
}
