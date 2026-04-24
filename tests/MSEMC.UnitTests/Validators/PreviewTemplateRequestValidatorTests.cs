using MSEMC.Configuration;
using MSEMC.Contracts.Requests;
using MSEMC.Validators;

namespace MSEMC.UnitTests.Validators;

/// <summary>
/// Testes do PreviewTemplateRequestValidator.
/// Cobre: data válido, data não-object, subject override dentro/fora do limite.
/// </summary>
public sealed class PreviewTemplateRequestValidatorTests
{
    private readonly PreviewTemplateRequestValidator _validator = new();

    // ── Happy Path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_ValidRequest_ShouldPass()
    {
        var data = JsonDocument.Parse("{\"nomeUsuario\":\"João\"}").RootElement;
        var request = new PreviewTemplateRequest(Data: data);

        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithLocaleAndSubjectOverride_ShouldPass()
    {
        var data = JsonDocument.Parse("{\"titulo\":\"Teste\"}").RootElement;
        var request = new PreviewTemplateRequest(
            Data: data,
            Locale: "pt-BR",
            SubjectOverride: "Assunto personalizado");

        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WithEmptyObject_ShouldPass()
    {
        // Objeto vazio é válido — a validação de variáveis obrigatórias é responsabilidade do service
        var data = JsonDocument.Parse("{}").RootElement;
        var request = new PreviewTemplateRequest(Data: data);

        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }

    // ── Data não é Object ─────────────────────────────────────────────────────

    [Fact]
    public async Task Validate_DataIsArray_ShouldFail()
    {
        var data = JsonDocument.Parse("[\"item1\", \"item2\"]").RootElement;
        var request = new PreviewTemplateRequest(Data: data);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Data");
    }

    [Fact]
    public async Task Validate_DataIsString_ShouldFail()
    {
        var data = JsonDocument.Parse("\"nao-e-objeto\"").RootElement;
        var request = new PreviewTemplateRequest(Data: data);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Data");
    }

    [Fact]
    public async Task Validate_DataIsNumber_ShouldFail()
    {
        var data = JsonDocument.Parse("42").RootElement;
        var request = new PreviewTemplateRequest(Data: data);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Data");
    }

    // ── SubjectOverride — Limite RFC 2822 ─────────────────────────────────────

    [Fact]
    public async Task Validate_SubjectOverrideExceedsMaxLength_ShouldFail()
    {
        var data = JsonDocument.Parse("{}").RootElement;
        var longSubject = new string('A', 999); // > 998 RFC limit
        var request = new PreviewTemplateRequest(Data: data, SubjectOverride: longSubject);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "SubjectOverride");
    }

    [Fact]
    public async Task Validate_SubjectOverrideAtMaxLength_ShouldPass()
    {
        var data = JsonDocument.Parse("{}").RootElement;
        var validSubject = new string('A', 998); // = 998 (limite exato)
        var request = new PreviewTemplateRequest(Data: data, SubjectOverride: validSubject);

        var result = await _validator.ValidateAsync(request);
        result.IsValid.Should().BeTrue();
    }
}
