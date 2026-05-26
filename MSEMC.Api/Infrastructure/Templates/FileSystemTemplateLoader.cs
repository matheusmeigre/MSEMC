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

    public async Task<Result<IReadOnlyList<TemplateSummary>>> ListAsync(
        string? locale = null,
        string? domain = null,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"template:catalog:{locale ?? _options.DefaultLocale}:{domain ?? "*"}";

        if (_options.CacheExpirationSeconds > 0 && cache.TryGetValue(cacheKey, out IReadOnlyList<TemplateSummary>? cached))
            return Result<IReadOnlyList<TemplateSummary>>.Ok(cached!);

        var basePath = Path.IsPathRooted(_options.BasePath)
            ? _options.BasePath
            : Path.Combine(AppContext.BaseDirectory, _options.BasePath);

        var localePath = Path.Combine(basePath, locale ?? _options.DefaultLocale);
        var searchPath = Directory.Exists(localePath)
            ? localePath
            : Path.Combine(basePath, _options.DefaultLocale);

        if (!Directory.Exists(searchPath))
        {
            logger.LogWarning("Diretório de templates não encontrado: '{SearchPath}'", searchPath);
            return Result<IReadOnlyList<TemplateSummary>>.Ok(Array.Empty<TemplateSummary>());
        }

        var results = new List<TemplateSummary>();

        var metaFiles = Directory.EnumerateFiles(searchPath, "*.meta.json", SearchOption.AllDirectories);

        foreach (var filePath in metaFiles)
        {
            // Ignora layouts internos (_layouts/)
            if (filePath.Contains($"{Path.DirectorySeparatorChar}_layouts{Path.DirectorySeparatorChar}") ||
                filePath.Contains($"/_layouts/"))
                continue;

            try
            {
                // Deriva o templateId relativo ao searchPath (sem extensão .meta.json)
                var relativePath = Path.GetRelativePath(searchPath, filePath);
                var templateId = relativePath
                    .Replace(".meta.json", string.Empty)
                    .Replace('\\', '/');

                await using var stream = File.OpenRead(filePath);
                var meta = await JsonSerializer.DeserializeAsync<TemplateMetadataJson>(
                    stream, JsonOptions, cancellationToken);

                if (meta is null) continue;

                // Aplica filtro por domínio se especificado
                if (domain is not null && !string.Equals(meta.Domain, domain, StringComparison.OrdinalIgnoreCase))
                    continue;

                var requiredVars = meta.RequiredVariables ?? [];
                var optionalVars = meta.OptionalVariables ?? [];

                results.Add(new TemplateSummary
                {
                    TemplateId = templateId,
                    Name = meta.Name,
                    Description = meta.Description,
                    Domain = meta.Domain,
                    SubjectTemplate = meta.SubjectTemplate,
                    RequiredVariables = requiredVars,
                    OptionalVariables = optionalVars,
                    ExamplePayload = BuildExamplePayload(requiredVars, optionalVars)
                });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao ler metadados do arquivo '{FilePath}' durante a listagem — ignorado", filePath);
            }
        }

        var ordered = results
            .OrderBy(t => t.Domain)
            .ThenBy(t => t.Name)
            .ToList()
            .AsReadOnly();

        if (_options.CacheExpirationSeconds > 0)
            cache.Set(cacheKey, (IReadOnlyList<TemplateSummary>)ordered, TimeSpan.FromSeconds(_options.CacheExpirationSeconds));

        logger.LogInformation("Catálogo de templates: {Count} template(s) encontrado(s) em '{Path}'", ordered.Count, searchPath);

        return Result<IReadOnlyList<TemplateSummary>>.Ok(ordered);
    }

    /// <summary>
    /// Gera um payload de exemplo com valores placeholder para cada variável.
    /// Facilita o copy-paste no Swagger ou painel de preview.
    /// </summary>
    private static Dictionary<string, object?> BuildExamplePayload(
        IList<string> required,
        IList<string> optional)
    {
        var payload = new Dictionary<string, object?>();

        foreach (var variable in required)
            payload[variable] = InferExampleValue(variable);

        foreach (var variable in optional)
            payload[variable] = InferExampleValue(variable);

        return payload;
    }

    private static object InferExampleValue(string variableName)
    {
        var lower = variableName.ToLowerInvariant();

        if (lower.Contains("email")) return "usuario@example.com";
        if (lower.Contains("link") || lower.Contains("url")) return "https://example.com/acao";
        if (lower.Contains("codigo") || lower.Contains("otp")) return "482916";
        if (lower.Contains("data") && lower.Contains("hora")) return "24/04/2026 às 14:35";
        if (lower.Contains("data") || lower.Contains("date")) return "24/04/2026";
        if (lower.Contains("hora") || lower.Contains("time")) return "14:35";
        if (lower.Contains("validade") || lower.Contains("minuto")) return 10;
        if (lower.Contains("hora") && lower.Contains("validade")) return 24;
        if (lower.Contains("valor") || lower.Contains("total")) return "R$ 149,90";
        if (lower.Contains("numero") || lower.Contains("número")) return "12345";
        if (lower.Contains("dias")) return 3;
        if (lower.Contains("itens")) return new[] { new { nome = "Produto Exemplo", quantidade = 1, valor = "R$ 149,90" } };
        if (lower.Contains("empresa")) return "Minha Empresa";
        if (lower.Contains("usuario") || lower.Contains("cliente") || lower.Contains("destinatario")) return "João Silva";
        if (lower.Contains("endereco") || lower.Contains("endereço")) return "Rua Exemplo, 123 — São Paulo/SP";
        if (lower.Contains("relatorio") || lower.Contains("relatório")) return "Relatório Mensal";
        if (lower.Contains("descricao") || lower.Contains("descrição")) return "Descrição do item";
        if (lower.Contains("status") || lower.Contains("metodo") || lower.Contains("método")) return "Exemplo";
        if (lower.Contains("titulo") || lower.Contains("título")) return "Título do e-mail";
        if (lower.Contains("conteudo") || lower.Contains("conteúdo")) return "Conteúdo do e-mail aqui.";
        if (lower.Contains("ip")) return "192.168.1.100";
        if (lower.Contains("navegador") || lower.Contains("browser")) return "Chrome 124 / Windows";
        if (lower.Contains("localidade") || lower.Contains("cidade")) return "São Paulo, BR";
        if (lower.Contains("prazo")) return "5 a 10 dias úteis";
        if (lower.Contains("rastreio") || lower.Contains("tracking")) return "BR123456789BR";

        return $"[{variableName}]";
    }

    // ── Path resolution ───────────────────────────────────────────────────────

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

    // DTO intermediário para deserialização do JSON
    private sealed record TemplateMetadataJson(
        string Name,
        string Description,
        string Domain,
        string? SubjectTemplate,
        List<string>? RequiredVariables,
        List<string>? OptionalVariables);
}
