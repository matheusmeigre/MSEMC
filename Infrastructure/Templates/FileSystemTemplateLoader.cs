using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MSEMC.Abstractions;
using MSEMC.Configuration;
using MSEMC.Domain.Entities;
using MSEMC.Domain.Results;

namespace MSEMC.Infrastructure.Templates;

/// <summary>
/// Carrega templates HTML e metadados do sistema de arquivos local.
/// Suporta resolução de locale com fallback e cache em memória (IMemoryCache).
/// Registrado como Singleton — thread-safe via IMemoryCache.
/// </summary>
public sealed class FileSystemTemplateLoader(
    IOptions<TemplateOptions> options,
    IMemoryCache cache,
    ILogger<FileSystemTemplateLoader> logger) : ITemplateLoader
{
    private readonly TemplateOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<Result<string>> LoadContentAsync(
        string templateId,
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidTemplateId(templateId))
            return Result<string>.Fail($"Invalid templateId: '{templateId}'. Path traversal is not allowed.");

        var cacheKey = $"template:content:{locale ?? _options.DefaultLocale}:{templateId}";

        if (_options.CacheExpirationSeconds > 0 && cache.TryGetValue(cacheKey, out string? cached))
        {
            logger.LogDebug("Cache hit para template '{TemplateId}' locale '{Locale}'", templateId, locale);
            return Result<string>.Ok(cached!);
        }

        var (resolvedPath, resolvedLocale) = ResolvePath(templateId, locale, ".html");

        if (resolvedPath is null)
        {
            logger.LogWarning("Template '{TemplateId}' não encontrado para locale '{Locale}' nem no fallback '{Default}'",
                templateId, locale, _options.DefaultLocale);
            return Result<string>.Fail($"Template '{templateId}' not found for locale '{locale ?? _options.DefaultLocale}'.");
        }

        try
        {
            var content = await File.ReadAllTextAsync(resolvedPath, cancellationToken);

            if (_options.CacheExpirationSeconds > 0)
            {
                cache.Set(cacheKey, content, TimeSpan.FromSeconds(_options.CacheExpirationSeconds));
            }

            logger.LogDebug("Template '{TemplateId}' carregado de '{Path}' (locale: {Locale})",
                templateId, resolvedPath, resolvedLocale);

            return Result<string>.Ok(content);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao ler arquivo de template '{Path}'", resolvedPath);
            return Result<string>.Fail($"Failed to read template file: {ex.Message}");
        }
    }

    public async Task<Result<TemplateMetadata>> LoadMetadataAsync(
        string templateId,
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidTemplateId(templateId))
            return Result<TemplateMetadata>.Fail($"Invalid templateId: '{templateId}'.");

        var cacheKey = $"template:metadata:{locale ?? _options.DefaultLocale}:{templateId}";

        if (_options.CacheExpirationSeconds > 0 && cache.TryGetValue(cacheKey, out TemplateMetadata? cachedMeta))
        {
            return Result<TemplateMetadata>.Ok(cachedMeta!);
        }

        var (resolvedPath, _) = ResolvePath(templateId, locale, ".meta.json");

        if (resolvedPath is null)
        {
            return Result<TemplateMetadata>.Fail($"Metadata for template '{templateId}' not found.");
        }

        try
        {
            await using var stream = File.OpenRead(resolvedPath);
            var metadata = await JsonSerializer.DeserializeAsync<TemplateMetadataJson>(stream, JsonOptions, cancellationToken);

            if (metadata is null)
                return Result<TemplateMetadata>.Fail($"Failed to deserialize metadata for '{templateId}'.");

            var result = new TemplateMetadata
            {
                Name = metadata.Name,
                Description = metadata.Description,
                Domain = metadata.Domain,
                SubjectTemplate = metadata.SubjectTemplate,
                RequiredVariables = metadata.RequiredVariables ?? [],
                OptionalVariables = metadata.OptionalVariables ?? []
            };

            if (_options.CacheExpirationSeconds > 0)
            {
                cache.Set(cacheKey, result, TimeSpan.FromSeconds(_options.CacheExpirationSeconds));
            }

            return Result<TemplateMetadata>.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Falha ao ler metadados do template '{Path}'", resolvedPath);
            return Result<TemplateMetadata>.Fail($"Failed to read template metadata: {ex.Message}");
        }
    }

    /// <summary>
    /// Resolve o caminho físico do arquivo com suporte a fallback de locale.
    /// Tenta: Templates/{locale}/{templateId}{extension} → Templates/default/{templateId}{extension}
    /// </summary>
    private (string? Path, string Locale) ResolvePath(string templateId, string? locale, string extension)
    {
        var basePath = Path.IsPathRooted(_options.BasePath)
            ? _options.BasePath
            : Path.Combine(AppContext.BaseDirectory, _options.BasePath);

        var requestedLocale = locale ?? _options.DefaultLocale;
        var candidates = new[]
        {
            (Path: Path.Combine(basePath, requestedLocale, $"{templateId}{extension}"), Locale: requestedLocale),
            (Path: Path.Combine(basePath, _options.DefaultLocale, $"{templateId}{extension}"), Locale: _options.DefaultLocale)
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate.Path))
            {
                logger.LogDebug("Resolvido: '{TemplatePath}'", candidate.Path);
                return (candidate.Path, candidate.Locale);
            }
        }

        return (null, _options.DefaultLocale);
    }

    /// <summary>
    /// Valida o templateId contra path traversal (ex: "../../etc/passwd").
    /// Permite apenas: letras, dígitos, hífens, underscores e slash simples para subdiretórios.
    /// </summary>
    private static bool IsValidTemplateId(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId)) return false;
        if (templateId.Contains("..")) return false;
        if (templateId.Contains('\\')) return false;

        return templateId.All(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '/');
    }

    // DTO intermediário para deserialização do JSON (evita dependência do record de domínio no sistema de arquivos)
    private sealed record TemplateMetadataJson(
        string Name,
        string Description,
        string Domain,
        string? SubjectTemplate,
        List<string>? RequiredVariables,
        List<string>? OptionalVariables);
}
