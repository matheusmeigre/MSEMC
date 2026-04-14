using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using MSEMC.Abstractions;
using MSEMC.Contracts.Requests;
using MSEMC.Contracts.Responses;
using MSEMC.Domain.Entities;

namespace MSEMC.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class EmailController : ControllerBase
{
    private readonly IEmailSender _emailSender;
    private readonly IValidator<SendEmailRequest> _validator;
    private readonly ILogger<EmailController> _logger;

    public EmailController(
        IEmailSender emailSender,
        IValidator<SendEmailRequest> validator,
        ILogger<EmailController> logger)
    {
        _emailSender = emailSender;
        _validator = validator;
        _logger = logger;
    }

    /// <summary>
    /// Sends an email message to the specified recipient.
    /// </summary>
    /// <param name="request">Email details including recipient, subject, and body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Accepted response with message tracking ID.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(SendEmailResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendEmail(
        [FromBody] SendEmailRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await _validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(
                new ValidationProblemDetails(
                    validationResult.Errors
                        .GroupBy(e => e.PropertyName)
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray())));
        }

        var message = EmailMessage.Create(
            recipient: request.Recipient,
            subject: request.Subject,
            body: request.Body,
            isHtml: request.IsHtml,
            ccRecipients: request.CcRecipients,
            bccRecipients: request.BccRecipients);

        _logger.LogInformation(
            "Received email request for {Recipient} (MessageId: {MessageId})",
            request.Recipient, message.Id);

        var result = await _emailSender.SendAsync(message, cancellationToken);

        return result.Match<IActionResult>(
            onSuccess: sent => Accepted(
                $"/api/email/{sent.Id}",
                new SendEmailResponse(
                    MessageId: sent.Id,
                    Status: sent.Status.ToString(),
                    AcceptedAt: sent.CreatedAt)),
            onFailure: error => Problem(
                detail: error,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Email Delivery Failed"));
    }
}
