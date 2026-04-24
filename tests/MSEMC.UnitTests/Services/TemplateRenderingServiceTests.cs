using MSEMC.Abstractions;
using MSEMC.Domain.Entities;
using MSEMC.Domain.Results;
using MSEMC.Infrastructure.Templates;
using MSEMC.Services;

namespace MSEMC.UnitTests.Services;

/// <summary>
/// Testes unitários para o TemplateRenderingService.
/// Usa NSubstitute para mockar ITemplateLoader e ITemplateEngine.
/// Valida o pipeline completo: Load→Validate→Render body e subject.
/// </summary>
public sealed class TemplateRenderingServiceTests
{
    private readonly ITemplateLoader _loader;
    private readonly ITemplateEngine _engine;
    private readonly TemplateVariableValidator _validator;
    private readonly TemplateRenderingService _service;

    public TemplateRenderingServiceTests()
    {
        _loader = Substitute.For<ITemplateLoader>();
        _engine = Substitute.For<ITemplateEngine>();
        _validator = new TemplateVariableValidator();
        var logger = Substitute.For<ILogger<TemplateRenderingService>>();
        _service = new TemplateRenderingService(_loader, _engine, _validator, logger);
    }

    private static JsonElement ToJsonElement(object obj)
        => JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(obj)).RootElement;

    // ── Happy Path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_ValidTemplateAndData_ShouldReturnRenderedHtmlAndSubject()
    {
        // Arrange
        var metadata = new TemplateMetadata
        {
            Name = "OTP", Description = "desc", Domain = "auth",
            SubjectTemplate = "Seu código: {{ codigoSeguranca }}",
            RequiredVariables = ["nomeUsuario", "codigoSeguranca", "validadeMinutos"]
        };

        _loader.LoadContentAsync("autenticacao/codigo-seguranca", "pt-BR", default)
            .Returns(Result<string>.Ok("<html>{{ nomeUsuario }} — {{ codigoSeguranca }}</html>"));
        _loader.LoadMetadataAsync("autenticacao/codigo-seguranca", "pt-BR", default)
            .Returns(Result<TemplateMetadata>.Ok(metadata));

        _engine.RenderAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, object?>>(), default)
            .Returns(ci => Result<string>.Ok($"RENDERED:{ci.Arg<string>()}"));

        var data = ToJsonElement(new { nomeUsuario = "João", codigoSeguranca = "123456", validadeMinutos = 10 });

        // Act
        var result = await _service.RenderAsync("autenticacao/codigo-seguranca", "pt-BR", data);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.RenderedHtml.Should().StartWith("RENDERED:");
        result.Value.ResolvedSubject.Should().StartWith("RENDERED:");
        result.Value.ResolvedLocale.Should().Be("pt-BR");
    }

    [Fact]
    public async Task RenderAsync_WithSubjectOverride_ShouldUseOverrideInsteadOfMetadata()
    {
        // Arrange
        _loader.LoadContentAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<string>.Ok("<html>{{ nomeUsuario }}</html>"));
        _loader.LoadMetadataAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<TemplateMetadata>.Ok(new TemplateMetadata
            {
                Name = "OTP", Description = "d", Domain = "auth",
                SubjectTemplate = "Template subject",
                RequiredVariables = ["nomeUsuario"]
            }));
        _engine.RenderAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, object?>>(), default)
            .Returns(Result<string>.Ok("RENDERED HTML"));

        var data = ToJsonElement(new { nomeUsuario = "João" });

        // Act
        var result = await _service.RenderAsync("otp", null, data, subjectOverride: "Override Subject");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ResolvedSubject.Should().Be("Override Subject");
    }

    [Fact]
    public async Task RenderAsync_NoSubjectTemplateAndNoOverride_ShouldFallbackToMetadataName()
    {
        // Arrange
        _loader.LoadContentAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<string>.Ok("<html>{{ nomeUsuario }}</html>"));
        _loader.LoadMetadataAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<TemplateMetadata>.Ok(new TemplateMetadata
            {
                Name = "Boas-vindas", Description = "d", Domain = "onboarding",
                SubjectTemplate = null, // sem subject template
                RequiredVariables = ["nomeUsuario"]
            }));
        _engine.RenderAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, object?>>(), default)
            .Returns(Result<string>.Ok("RENDERED HTML"));

        var data = ToJsonElement(new { nomeUsuario = "João" });

        // Act
        var result = await _service.RenderAsync("boas-vindas", null, data);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value!.ResolvedSubject.Should().Be("Boas-vindas");
    }

    // ── Fail: Template não encontrado ─────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_TemplateNotFound_ShouldReturnFailResult()
    {
        _loader.LoadContentAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<string>.Fail("Template 'inexistente' not found for locale 'default'."));

        var data = ToJsonElement(new { });

        var result = await _service.RenderAsync("inexistente", null, data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("not found");
    }

    // ── Fail: Metadata não encontrado ─────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_MetadataNotFound_ShouldReturnFailResult()
    {
        _loader.LoadContentAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<string>.Ok("<html>ok</html>"));
        _loader.LoadMetadataAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<TemplateMetadata>.Fail("Metadata not found."));

        var data = ToJsonElement(new { });

        var result = await _service.RenderAsync("template", null, data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Metadata not found");
    }

    // ── Fail: Variáveis obrigatórias faltando ─────────────────────────────────

    [Fact]
    public async Task RenderAsync_MissingRequiredVariables_ShouldReturnFailResultWithVariableNames()
    {
        _loader.LoadContentAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<string>.Ok("<html>{{ nomeUsuario }} {{ codigoSeguranca }}</html>"));
        _loader.LoadMetadataAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<TemplateMetadata>.Ok(new TemplateMetadata
            {
                Name = "OTP", Description = "d", Domain = "auth",
                RequiredVariables = ["nomeUsuario", "codigoSeguranca", "validadeMinutos"]
            }));

        // Envia apenas nomeUsuario — faltam codigoSeguranca e validadeMinutos
        var data = ToJsonElement(new { nomeUsuario = "João" });

        var result = await _service.RenderAsync("autenticacao/codigo-seguranca", null, data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("codigoSeguranca").And.Contain("validadeMinutos");
    }

    // ── Fail: Engine retorna erro ─────────────────────────────────────────────

    [Fact]
    public async Task RenderAsync_EngineFailure_ShouldReturnFailResult()
    {
        _loader.LoadContentAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<string>.Ok("<html>{{ nomeUsuario }}</html>"));
        _loader.LoadMetadataAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<TemplateMetadata>.Ok(new TemplateMetadata
            {
                Name = "OTP", Description = "d", Domain = "auth",
                RequiredVariables = ["nomeUsuario"]
            }));
        _engine.RenderAsync(Arg.Any<string>(), Arg.Any<IDictionary<string, object?>>(), default)
            .Returns(Result<string>.Fail("Template syntax error: unexpected token"));

        var data = ToJsonElement(new { nomeUsuario = "João" });

        var result = await _service.RenderAsync("template", null, data);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("syntax error");
    }

    // ── Conversão JsonElement: tipos complexos ────────────────────────────────

    [Fact]
    public async Task RenderAsync_WithNestedObjectInData_ShouldConvertCorrectly()
    {
        _loader.LoadContentAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<string>.Ok("<html>{{ usuario.nome }}</html>"));
        _loader.LoadMetadataAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<TemplateMetadata>.Ok(new TemplateMetadata
            {
                Name = "X", Description = "d", Domain = "auth",
                RequiredVariables = ["usuario"]
            }));

        IDictionary<string, object?>? capturedData = null;

        _engine.RenderAsync(
            Arg.Any<string>(),
            Arg.Do<IDictionary<string, object?>>(d => capturedData = d),
            default)
            .Returns(Result<string>.Ok("RENDERED"));

        var data = ToJsonElement(new { usuario = new { nome = "João", email = "joao@email.com" } });

        _ = await _service.RenderAsync("template", null, data);

        // O dicionário deve conter "usuario" como nested dicionary
        capturedData.Should().ContainKey("usuario");
        capturedData!["usuario"].Should().BeAssignableTo<IDictionary<string, object?>>();
    }

    [Fact]
    public async Task RenderAsync_WithArrayInData_ShouldConvertToList()
    {
        _loader.LoadContentAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<string>.Ok("<html>{{ for item in itens }}{{ item.nome }}{{ end }}</html>"));
        _loader.LoadMetadataAsync(Arg.Any<string>(), Arg.Any<string?>(), default)
            .Returns(Result<TemplateMetadata>.Ok(new TemplateMetadata
            {
                Name = "X", Description = "d", Domain = "ecommerce",
                RequiredVariables = ["itens"]
            }));

        IDictionary<string, object?>? capturedData = null;

        _engine.RenderAsync(
            Arg.Any<string>(),
            Arg.Do<IDictionary<string, object?>>(d => capturedData = d),
            default)
            .Returns(Result<string>.Ok("RENDERED"));

        var data = ToJsonElement(new
        {
            itens = new[]
            {
                new { nome = "Produto A", valor = 10.0 },
                new { nome = "Produto B", valor = 20.0 }
            }
        });

        _ = await _service.RenderAsync("template", null, data);

        capturedData.Should().ContainKey("itens");
        capturedData!["itens"].Should().BeAssignableTo<System.Collections.IList>();
    }
}
