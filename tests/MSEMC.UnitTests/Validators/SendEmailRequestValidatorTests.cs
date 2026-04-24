using FluentAssertions;
using Microsoft.Extensions.Options;
using MSEMC.Configuration;
using MSEMC.Contracts.Requests;
using MSEMC.Validators;
using System.Text.Json;

namespace MSEMC.UnitTests.Validators;

public sealed class SendEmailRequestValidatorTests
{
    private readonly SendEmailRequestValidator _validator;

    public SendEmailRequestValidatorTests()
    {
        var opts = Options.Create(new TemplateOptions
        {
            BasePath = "Templates",
            MaxAttachmentsPerEmail = 5,
            MaxAttachmentSizeBytes = 10 * 1024 * 1024
        });
        _validator = new SendEmailRequestValidator(opts);
    }

    // ── Raw Mode — Valid Requests ──────────────────────────────────────────────

    [Fact]
    public async Task Validate_RawMode_ValidRequest_ShouldPass()
    {
        var request = new SendEmailRequest(
            Recipient: "test@email.com",
            Subject: "Subject",
            Body: "Body");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_RawMode_WithCcAndBcc_ShouldPass()
    {
        var request = new SendEmailRequest(
            Recipient: "test@email.com",
            Subject: "Subject",
            Body: "Body",
            CcRecipients: ["cc1@email.com", "cc2@email.com"],
            BccRecipients: ["bcc@email.com"]);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    // ── Recipient Validation ───────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_EmptyRecipient_ShouldFail(string? recipient)
    {
        var request = new SendEmailRequest(
            Recipient: recipient!,
            Subject: "Subject",
            Body: "Body");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@missing-local")]
    [InlineData("missing-domain@")]
    public async Task Validate_InvalidRecipientEmail_ShouldFail(string recipient)
    {
        var request = new SendEmailRequest(
            Recipient: recipient,
            Subject: "Subject",
            Body: "Body");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient");
    }

    [Fact]
    public async Task Validate_InvalidCcEmail_ShouldFail()
    {
        var request = new SendEmailRequest(
            Recipient: "test@email.com",
            Subject: "Subject",
            Body: "Body",
            CcRecipients: ["valid@email.com", "invalid-email"]);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("CcRecipients"));
    }

    [Fact]
    public async Task Validate_InvalidBccEmail_ShouldFail()
    {
        var request = new SendEmailRequest(
            Recipient: "test@email.com",
            Subject: "Subject",
            Body: "Body",
            BccRecipients: ["not-valid"]);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("BccRecipients"));
    }

    // ── Raw Mode Validation ────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_RawMode_EmptySubject_ShouldFail(string? subject)
    {
        var request = new SendEmailRequest(
            Recipient: "test@email.com",
            Subject: subject,
            Body: "Body");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Subject");
    }

    [Fact]
    public async Task Validate_RawMode_SubjectExceedsMaxLength_ShouldFail()
    {
        var longSubject = new string('A', 999);
        var request = new SendEmailRequest(
            Recipient: "test@email.com",
            Subject: longSubject,
            Body: "Body");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Subject");
    }

    // ── Mutual Exclusion Rules ─────────────────────────────────────────────────

    [Fact]
    public async Task Validate_TemplateIdAndBody_BothProvided_ShouldFail()
    {
        var data = JsonDocument.Parse("{\"nomeUsuario\":\"João\"}").RootElement;
        var request = new SendEmailRequest(
            Recipient: "test@email.com",
            TemplateId: "autenticacao/codigo-seguranca",
            Data: data,
            Body: "Raw body — não pode coexistir com templateId");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("mutually exclusive"));
    }

    [Fact]
    public async Task Validate_NeitherTemplateIdNorBody_ShouldFail()
    {
        var request = new SendEmailRequest(
            Recipient: "test@email.com");

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage.Contains("templateId") || e.ErrorMessage.Contains("body"));
    }

    // ── Template Mode Validation ───────────────────────────────────────────────

    [Fact]
    public async Task Validate_TemplateMode_ValidRequest_ShouldPass()
    {
        var data = JsonDocument.Parse("{\"nomeUsuario\":\"João\",\"codigoSeguranca\":\"123456\",\"validadeMinutos\":10}").RootElement;
        var request = new SendEmailRequest(
            Recipient: "test@email.com",
            TemplateId: "autenticacao/codigo-seguranca",
            Data: data);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_TemplateMode_PathTraversalTemplateId_ShouldFail()
    {
        var data = JsonDocument.Parse("{}").RootElement;
        var request = new SendEmailRequest(
            Recipient: "test@email.com",
            TemplateId: "../../etc/passwd",
            Data: data);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "TemplateId");
    }

    [Fact]
    public async Task Validate_TemplateMode_DataNotObject_ShouldFail()
    {
        var data = JsonDocument.Parse("\"apenas-string\"").RootElement;
        var request = new SendEmailRequest(
            Recipient: "test@email.com",
            TemplateId: "autenticacao/codigo-seguranca",
            Data: data);

        var result = await _validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Data");
    }
}
