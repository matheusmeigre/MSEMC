using MSEMC.Domain.Entities;
using MSEMC.Domain.Enums;

namespace MSEMC.UnitTests.Domain;

public sealed class EmailMessageTests
{
    // ── Factory Method Tests ───────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidParams_ShouldReturnMessageWithPendingStatus()
    {
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");

        message.Recipient.Should().Be("test@email.com");
        message.Subject.Should().Be("Subject");
        message.Body.Should().Be("Body");
        message.Status.Should().Be(EmailStatus.Pending);
        message.IsHtml.Should().BeTrue();
        message.Id.Should().NotBeEmpty();
        message.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        message.SentAt.Should().BeNull();
        message.ErrorMessage.Should().BeNull();
        message.Attachments.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithOptionalParams_ShouldSetCcBccAndAttachments()
    {
        var cc = new List<string> { "cc@email.com" };
        var bcc = new List<string> { "bcc@email.com" };
        var attachments = new List<EmailAttachment>
        {
            new() { FileName = "file.pdf", ContentType = "application/pdf", ContentBase64 = Convert.ToBase64String([1, 2, 3]) }
        };

        var message = EmailMessage.Create(
            "test@email.com", "Subject", "Body",
            isHtml: false,
            ccRecipients: cc,
            bccRecipients: bcc,
            attachments: attachments);

        message.IsHtml.Should().BeFalse();
        message.CcRecipients.Should().ContainSingle().Which.Should().Be("cc@email.com");
        message.BccRecipients.Should().ContainSingle().Which.Should().Be("bcc@email.com");
        message.Attachments.Should().ContainSingle()
            .Which.FileName.Should().Be("file.pdf");
    }

    [Theory]
    [InlineData(null, "Subject", "Body")]
    [InlineData("test@email.com", null, "Body")]
    [InlineData("test@email.com", "Subject", null)]
    public void Create_WithNullRequiredParam_ShouldThrowArgumentNullException(
        string? recipient, string? subject, string? body)
    {
        var act = () => EmailMessage.Create(recipient!, subject!, body!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_WithoutAttachments_ShouldDefaultToEmptyList()
    {
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        message.Attachments.Should().NotBeNull().And.BeEmpty();
    }

    // ── State Transition Tests ─────────────────────────────────────────────────

    [Fact]
    public void MarkAsSending_FromPending_ShouldTransitionToSending()
    {
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        message.MarkAsSending();
        message.Status.Should().Be(EmailStatus.Sending);
    }

    [Fact]
    public void MarkAsSent_FromSending_ShouldTransitionToSentWithTimestamp()
    {
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        message.MarkAsSending();
        message.MarkAsSent();

        message.Status.Should().Be(EmailStatus.Sent);
        message.SentAt.Should().NotBeNull()
            .And.BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
        message.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void MarkAsSent_FromPending_ShouldThrowInvalidOperationException()
    {
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        var act = () => message.MarkAsSent();
        act.Should().Throw<InvalidOperationException>().WithMessage("*Pending*Sent*");
    }

    [Fact]
    public void MarkAsFailed_ShouldSetErrorMessage()
    {
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        message.MarkAsFailed("SMTP timeout");

        message.Status.Should().Be(EmailStatus.Failed);
        message.ErrorMessage.Should().Be("SMTP timeout");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkAsFailed_WithNullOrEmptyError_ShouldThrowArgumentException(string? error)
    {
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        var act = () => message.MarkAsFailed(error!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void MarkAsQueued_FromPending_ShouldTransitionToQueued()
    {
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        message.MarkAsQueued();
        message.Status.Should().Be(EmailStatus.Queued);
    }

    [Fact]
    public void MarkAsQueued_FromSending_ShouldThrowInvalidOperationException()
    {
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        message.MarkAsSending();
        var act = () => message.MarkAsQueued();
        act.Should().Throw<InvalidOperationException>();
    }
}
