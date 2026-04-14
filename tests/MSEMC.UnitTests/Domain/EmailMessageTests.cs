using FluentAssertions;
using MSEMC.Domain.Entities;
using MSEMC.Domain.Enums;

namespace MSEMC.UnitTests.Domain;

public sealed class EmailMessageTests
{
    // ── Factory Method Tests ──

    [Fact]
    public void Create_WithValidParams_ShouldReturnMessageWithPendingStatus()
    {
        // Arrange & Act
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");

        // Assert
        message.Recipient.Should().Be("test@email.com");
        message.Subject.Should().Be("Subject");
        message.Body.Should().Be("Body");
        message.Status.Should().Be(EmailStatus.Pending);
        message.IsHtml.Should().BeTrue();
        message.Id.Should().NotBeEmpty();
        message.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        message.SentAt.Should().BeNull();
        message.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Create_WithOptionalParams_ShouldSetCcAndBcc()
    {
        // Arrange
        var cc = new List<string> { "cc@email.com" };
        var bcc = new List<string> { "bcc@email.com" };

        // Act
        var message = EmailMessage.Create(
            "test@email.com", "Subject", "Body",
            isHtml: false, ccRecipients: cc, bccRecipients: bcc);

        // Assert
        message.IsHtml.Should().BeFalse();
        message.CcRecipients.Should().ContainSingle().Which.Should().Be("cc@email.com");
        message.BccRecipients.Should().ContainSingle().Which.Should().Be("bcc@email.com");
    }

    [Theory]
    [InlineData(null, "Subject", "Body")]
    [InlineData("test@email.com", null, "Body")]
    [InlineData("test@email.com", "Subject", null)]
    public void Create_WithNullRequiredParam_ShouldThrowArgumentNullException(
        string? recipient, string? subject, string? body)
    {
        // Act
        var act = () => EmailMessage.Create(recipient!, subject!, body!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    // ── State Transition Tests ──

    [Fact]
    public void MarkAsSending_FromPending_ShouldTransitionToSending()
    {
        // Arrange
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");

        // Act
        message.MarkAsSending();

        // Assert
        message.Status.Should().Be(EmailStatus.Sending);
    }

    [Fact]
    public void MarkAsSent_FromSending_ShouldTransitionToSentWithTimestamp()
    {
        // Arrange
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        message.MarkAsSending();

        // Act
        message.MarkAsSent();

        // Assert
        message.Status.Should().Be(EmailStatus.Sent);
        message.SentAt.Should().NotBeNull();
        message.SentAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        message.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkAsSent_FromPending_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");

        // Act
        var act = () => message.MarkAsSent();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Pending*Sent*");
    }

    [Fact]
    public void MarkAsFailed_ShouldSetErrorMessage()
    {
        // Arrange
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");

        // Act
        message.MarkAsFailed("SMTP timeout");

        // Assert
        message.Status.Should().Be(EmailStatus.Failed);
        message.ErrorMessage.Should().Be("SMTP timeout");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkAsFailed_WithNullOrEmptyError_ShouldThrowArgumentException(string? error)
    {
        // Arrange
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");

        // Act
        var act = () => message.MarkAsFailed(error!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsQueued_FromPending_ShouldTransitionToQueued()
    {
        // Arrange
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");

        // Act
        message.MarkAsQueued();

        // Assert
        message.Status.Should().Be(EmailStatus.Queued);
    }

    [Fact]
    public void MarkAsQueued_FromSending_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        message.MarkAsSending();

        // Act
        var act = () => message.MarkAsQueued();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
