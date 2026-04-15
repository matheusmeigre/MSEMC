using FluentValidation;
using MSEMC.Contracts.Requests;

namespace MSEMC.Validators;

/// <summary>
/// Valida o SendEmailRequest usando regras do FluentValidation.
/// Aplica restrições da RFC 2822 e regras de negócio.
/// </summary>
public sealed class SendEmailRequestValidator : AbstractValidator<SendEmailRequest>
{
    private const int MaxSubjectLength = 998; // RFC 2822 line length limit
    private const int MaxBodyLengthBytes = 10 * 1024 * 1024; // 10 MB

    public SendEmailRequestValidator()
    {
        RuleFor(x => x.Recipient)
            .NotEmpty().WithMessage("Recipient is required")
            .EmailAddress().WithMessage("Recipient must be a valid email address");

        RuleFor(x => x.Subject)
            .NotEmpty().WithMessage("Subject is required")
            .MaximumLength(MaxSubjectLength)
            .WithMessage($"Subject must not exceed {MaxSubjectLength} characters");

        RuleFor(x => x.Body)
            .NotEmpty().WithMessage("Body is required")
            .Must(body => body is null || System.Text.Encoding.UTF8.GetByteCount(body) <= MaxBodyLengthBytes)
            .WithMessage("Body must not exceed 10 MB");

        RuleForEach(x => x.CcRecipients)
            .EmailAddress().WithMessage("Each CC recipient must be a valid email address")
            .When(x => x.CcRecipients is { Count: > 0 });

        RuleForEach(x => x.BccRecipients)
            .EmailAddress().WithMessage("Each BCC recipient must be a valid email address")
            .When(x => x.BccRecipients is { Count: > 0 });
    }
}
