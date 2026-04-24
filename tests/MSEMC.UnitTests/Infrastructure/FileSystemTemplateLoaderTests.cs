using MSEMC.Configuration;
using MSEMC.Domain.Entities;
using MSEMC.Infrastructure.Templates;

namespace MSEMC.UnitTests.Infrastructure;

/// <summary>
/// Testes unitários para o FileSystemTemplateLoader.
/// Usa um diretório temporário real para simular o filesystem sem mocks pesados.
/// </summary>
public sealed class FileSystemTemplateLoaderTests : IDisposable
{
    private readonly string _basePath;
    private readonly IMemoryCache _memoryCache;
    private readonly FileSystemTemplateLoader _loader;

    public FileSystemTemplateLoaderTests()
    {
        _basePath = Path.Combine(Path.GetTempPath(), $"msemc-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(_basePath, "default", "autenticacao"));
        Directory.CreateDirectory(Path.Combine(_basePath, "pt-BR", "autenticacao"));

        _memoryCache = new MemoryCache(new MemoryCacheOptions());

        var options = Options.Create(new TemplateOptions
        {
            BasePath = _basePath,
            DefaultLocale = "default",
            CacheExpirationSeconds = 300,
            MaxAttachmentSizeBytes = 10 * 1024 * 1024,
            MaxAttachmentsPerEmail = 5
        });

        var logger = Substitute.For<ILogger<FileSystemTemplateLoader>>();
        _loader = new FileSystemTemplateLoader(options, _memoryCache, logger);
    }

    public void Dispose()
    {
        _memoryCache.Dispose();
        if (Directory.Exists(_basePath))
            Directory.Delete(_basePath, recursive: true);
    }

    // ── LoadContentAsync: Happy Path ──────────────────────────────────────────

    [Fact]
    public async Task LoadContentAsync_ExistingTemplate_ShouldReturnContent()
    {
        var templateContent = "<html><body>Olá, {{ nomeUsuario }}!</body></html>";
        var templatePath = Path.Combine(_basePath, "default", "autenticacao", "codigo-seguranca.html");
        await File.WriteAllTextAsync(templatePath, templateContent);

        var result = await _loader.LoadContentAsync("autenticacao/codigo-seguranca", "default");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(templateContent);
    }

    [Fact]
    public async Task LoadContentAsync_RequestedLocale_ShouldLoadLocaleFile()
    {
        var ptBrContent = "<html>PT-BR</html>";
        var ptBrPath = Path.Combine(_basePath, "pt-BR", "autenticacao", "codigo-seguranca.html");
        await File.WriteAllTextAsync(ptBrPath, ptBrContent);

        var defaultContent = "<html>DEFAULT</html>";
        var defaultPath = Path.Combine(_basePath, "default", "autenticacao", "codigo-seguranca.html");
        await File.WriteAllTextAsync(defaultPath, defaultContent);

        var result = await _loader.LoadContentAsync("autenticacao/codigo-seguranca", "pt-BR");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ptBrContent);
    }

    // ── LoadContentAsync: Fallback ────────────────────────────────────────────

    [Fact]
    public async Task LoadContentAsync_LocaleNotFound_ShouldFallbackToDefault()
    {
        var defaultContent = "<html>DEFAULT FALLBACK</html>";
        var defaultPath = Path.Combine(_basePath, "default", "autenticacao", "codigo-seguranca.html");
        await File.WriteAllTextAsync(defaultPath, defaultContent);

        // Não existe o locale "en-US"
        var result = await _loader.LoadContentAsync("autenticacao/codigo-seguranca", "en-US");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(defaultContent);
    }

    [Fact]
    public async Task LoadContentAsync_NullLocale_ShouldUseDefaultLocale()
    {
        var content = "<html>DEFAULT</html>";
        var path = Path.Combine(_basePath, "default", "autenticacao", "codigo-seguranca.html");
        await File.WriteAllTextAsync(path, content);

        var result = await _loader.LoadContentAsync("autenticacao/codigo-seguranca", null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(content);
    }

    // ── LoadContentAsync: Not Found ───────────────────────────────────────────

    [Fact]
    public async Task LoadContentAsync_TemplateNotFound_ShouldReturnFailResult()
    {
        var result = await _loader.LoadContentAsync("autenticacao/template-inexistente");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    // ── Path Traversal Guard ──────────────────────────────────────────────────

    [Theory]
    [InlineData("../../etc/passwd")]
    [InlineData("..\\windows\\system")]
    [InlineData("")]
    [InlineData("  ")]
    public async Task LoadContentAsync_InvalidTemplateId_ShouldReturnFailResult(string templateId)
    {
        var result = await _loader.LoadContentAsync(templateId);

        result.IsSuccess.Should().BeFalse();
        (result.Error!.Contains("Invalid templateId") || result.Error.Contains("not found")).Should().BeTrue(
            because: $"Expected error about invalid templateId or not found, but got: {result.Error}");
    }

    // ── LoadMetadataAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task LoadMetadataAsync_ValidMetaFile_ShouldReturnTypedMetadata()
    {
        var metaJson = """
            {
              "name": "Código de Segurança",
              "description": "OTP para 2FA",
              "domain": "autenticacao",
              "subjectTemplate": "Seu código: {{ codigoSeguranca }}",
              "requiredVariables": ["nomeUsuario", "codigoSeguranca", "validadeMinutos"],
              "optionalVariables": ["nomeEmpresa"]
            }
            """;

        var metaPath = Path.Combine(_basePath, "default", "autenticacao", "codigo-seguranca.meta.json");
        await File.WriteAllTextAsync(metaPath, metaJson);

        var result = await _loader.LoadMetadataAsync("autenticacao/codigo-seguranca");

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Código de Segurança");
        result.Value.Domain.Should().Be("autenticacao");
        result.Value.RequiredVariables.Should().HaveCount(3)
            .And.Contain("nomeUsuario")
            .And.Contain("codigoSeguranca")
            .And.Contain("validadeMinutos");
        result.Value.OptionalVariables.Should().ContainSingle().Which.Should().Be("nomeEmpresa");
    }

    [Fact]
    public async Task LoadMetadataAsync_MetaFileNotFound_ShouldReturnFailResult()
    {
        var result = await _loader.LoadMetadataAsync("autenticacao/template-sem-meta");

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
    }

    // ── Cache ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task LoadContentAsync_SecondCall_ShouldReturnFromCache()
    {
        var content = "<html>Cached</html>";
        var path = Path.Combine(_basePath, "default", "autenticacao", "codigo-seguranca.html");
        await File.WriteAllTextAsync(path, content);

        // Primeira chamada — popula o cache
        var result1 = await _loader.LoadContentAsync("autenticacao/codigo-seguranca");

        // Modifica o arquivo no disco
        await File.WriteAllTextAsync(path, "<html>Updated on disk</html>");

        // Segunda chamada — deve ainda retornar o conteúdo cacheado
        var result2 = await _loader.LoadContentAsync("autenticacao/codigo-seguranca");

        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result2.Value.Should().Be(content); // ainda o valor antigo (cache hit)
    }
}
