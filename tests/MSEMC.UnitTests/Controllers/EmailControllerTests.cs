using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using NSubstitute;
using MSEMC.Abstractions;
using MSEMC.Contracts.Requests;
using MSEMC.Controllers;
using MSEMC.Domain.Entities;
using MSEMC.Domain.Results;

namespace MSEMC.UnitTests.Controllers;

public sealed class EmailControllerTests
{
    private readonly IEmailSender _emailSender = Substitute.For<IEmailSender>();
    private readonly IValidator<SendEmailRequest> _validator = Substitute.For<IValidator<SendEmailRequest>>();
    private readonly ILogger<EmailController> _logger = Substitute.For<ILogger<EmailController>>();
    private readonly EmailController _controller;

    public EmailControllerTests()
    {
        _controller = new EmailController(_emailSender, _validator, _logger);
    }

    [Fact]
    public async Task SendEmail_WithValidRequest_ShouldReturn202Accepted()
    {
        // Arrange
        var request = new SendEmailRequest("test@email.com", "Subject", "Body");
        var message = EmailMessage.Create("test@email.com", "Subject", "Body");
        message.MarkAsSending();
        message.MarkAsSent();

        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result<EmailMessage>.Ok(message));

        // Act
        var result = await _controller.SendEmail(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<AcceptedResult>();
        var accepted = (AcceptedResult)result;
        accepted.StatusCode.Should().Be(StatusCodes.Status202Accepted);
    }

    [Fact]
    public async Task SendEmail_WithInvalidRequest_ShouldReturn400BadRequest()
    {
        // Arrange
        var request = new SendEmailRequest("", "", "");
        var validationResult = new ValidationResult(new[]
        {
            new ValidationFailure("Recipient", "Recipient is required"),
            new ValidationFailure("Subject", "Subject is required")
        });

        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(validationResult);

        // Act
        var result = await _controller.SendEmail(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
        var objectResult = (BadRequestObjectResult)result;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task SendEmail_WhenSenderFails_ShouldReturn500Problem()
    {
        // Arrange
        var request = new SendEmailRequest("test@email.com", "Subject", "Body");

        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(Result<EmailMessage>.Fail("SMTP connection refused"));

        // Act
        var result = await _controller.SendEmail(request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)result;
        objectResult.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task SendEmail_ShouldCallSenderWithCorrectData()
    {
        // Arrange
        var request = new SendEmailRequest(
            "test@email.com", "Test Subject", "<h1>Hello</h1>",
            IsHtml: true,
            CcRecipients: new List<string> { "cc@test.com" });

        _validator.ValidateAsync(request, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        _emailSender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var msg = callInfo.Arg<EmailMessage>();
                msg.MarkAsSending();
                msg.MarkAsSent();
                return Result<EmailMessage>.Ok(msg);
            });

        // Act
        await _controller.SendEmail(request, CancellationToken.None);

        // Assert
        await _emailSender.Received(1).SendAsync(
            Arg.Is<EmailMessage>(m =>
                m.Recipient == "test@email.com" &&
                m.Subject == "Test Subject" &&
                m.Body == "<h1>Hello</h1>" &&
                m.IsHtml == true &&
                m.CcRecipients.Count == 1),
            Arg.Any<CancellationToken>());
    }
}
