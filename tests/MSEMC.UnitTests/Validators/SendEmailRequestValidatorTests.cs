using FluentAssertions;
using MSEMC.Contracts.Requests;
using MSEMC.Validators;

namespace MSEMC.UnitTests.Validators;

public sealed class SendEmailRequestValidatorTests
{
    private readonly SendEmailRequestValidator _validator = new();

    // ── Valid Requests ──

    [Fact]
    public async Task Validate_ValidRequest_ShouldPass()
    {
        // Arrange
        var request = new SendEmailRequest("test@email.com", "Subject", "Body");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_ValidRequestWithCcAndBcc_ShouldPass()
    {
        // Arrange
        var request = new SendEmailRequest(
            "test@email.com", "Subject", "Body",
            CcRecipients: new List<string> { "cc1@email.com", "cc2@email.com" },
            BccRecipients: new List<string> { "bcc@email.com" });

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    // ── Recipient Validation ──

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_EmptyRecipient_ShouldFail(string? recipient)
    {
        // Arrange
        var request = new SendEmailRequest(recipient!, "Subject", "Body");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient");
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@missing-local")]
    [InlineData("missing-domain@")]
    public async Task Validate_InvalidRecipientEmail_ShouldFail(string recipient)
    {
        // Arrange
        var request = new SendEmailRequest(recipient, "Subject", "Body");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Recipient");
    }

    // ── Subject Validation ──

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_EmptySubject_ShouldFail(string? subject)
    {
        // Arrange
        var request = new SendEmailRequest("test@email.com", subject!, "Body");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Subject");
    }

    [Fact]
    public async Task Validate_SubjectExceedsMaxLength_ShouldFail()
    {
        // Arrange
        var longSubject = new string('A', 999); // > 998 RFC limit
        var request = new SendEmailRequest("test@email.com", longSubject, "Body");

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Subject");
    }

    // ── Body Validation ──

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Validate_EmptyBody_ShouldFail(string? body)
    {
        // Arrange
        var request = new SendEmailRequest("test@email.com", "Subject", body!);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Body");
    }

    // ── CC/BCC Validation ──

    [Fact]
    public async Task Validate_InvalidCcEmail_ShouldFail()
    {
        // Arrange
        var request = new SendEmailRequest(
            "test@email.com", "Subject", "Body",
            CcRecipients: new List<string> { "valid@email.com", "invalid-email" });

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("CcRecipients"));
    }

    [Fact]
    public async Task Validate_InvalidBccEmail_ShouldFail()
    {
        // Arrange
        var request = new SendEmailRequest(
            "test@email.com", "Subject", "Body",
            BccRecipients: new List<string> { "not-valid" });

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName.Contains("BccRecipients"));
    }

    [Fact]
    public async Task Validate_NullCcAndBcc_ShouldPass()
    {
        // Arrange
        var request = new SendEmailRequest(
            "test@email.com", "Subject", "Body",
            CcRecipients: null, BccRecipients: null);

        // Act
        var result = await _validator.ValidateAsync(request);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
