using FluentValidation;
using MSEMC.Configuration;
using MSEMC.Contracts.Requests;
using Microsoft.Extensions.Options;

namespace MSEMC.Validators;

/// <summary>
/// Valida o SendEmailRequest com todas as regras de negócio.
/// Aplica FluentValidation com regras condicionais para os modos Template e Raw.
/// </summary>
public sealed class SendEmailRequestValidator : AbstractValidator<SendEmailRequest>
{
    private const int MaxSubjectLength = 998; // RFC 2822
    private const int MaxBodyLengthBytes = 10 * 1024 * 1024; // 10 MB

    public SendEmailRequestValidator(IOptions<TemplateOptions> templateOptions)
    {
        var opts = templateOptions.Value;

        // ─── Destinatário ──────────────────────────────────────────────────────────
        RuleFor(x => x.Recipient)
            .NotEmpty().WithMessage("Recipient is required")
            .EmailAddress().WithMessage("Recipient must be a valid email address");

        RuleForEach(x => x.CcRecipients)
            .EmailAddress().WithMessage("Each CC recipient must be a valid email address")
            .When(x => x.CcRecipients is { Count: > 0 });

        RuleForEach(x => x.BccRecipients)
            .EmailAddress().WithMessage("Each BCC recipient must be a valid email address")
            .When(x => x.BccRecipients is { Count: > 0 });

        // ─── Exclusão mútua entre Modo Template e Modo Raw ───────────────────────
        RuleFor(x => x)
            .Must(x => !(x.TemplateId is not null && x.Body is not null))
            .WithMessage("'templateId' and 'body' are mutually exclusive. Use one or the other.")
            .WithName("Request");

        RuleFor(x => x)
            .Must(x => x.TemplateId is not null || x.Body is not null)
            .WithMessage("Either 'templateId' (Template Mode) or 'body' (Raw Mode) must be provided.")
            .WithName("Request");

        // ─── Modo Template ────────────────────────────────────────────────────────
        When(x => x.TemplateId is not null, () =>
        {
            RuleFor(x => x.TemplateId!)
                .NotEmpty().WithMessage("TemplateId cannot be empty when provided")
                .Must(id => !id.Contains("..") && !id.Contains('\\'))
                .WithMessage("TemplateId cannot contain path traversal characters ('..' or '\\')");

            RuleFor(x => x.Data)
                .Must(data => data.HasValue && data.Value.ValueKind == System.Text.Json.JsonValueKind.Object)
                .WithMessage("'data' is required and must be a JSON object when using Template Mode.");
        });

        // ─── Modo Raw ─────────────────────────────────────────────────────────────
        When(x => x.Body is not null && x.TemplateId is null, () =>
        {
            RuleFor(x => x.Subject)
                .NotEmpty().WithMessage("Subject is required in Raw Mode")
                .MaximumLength(MaxSubjectLength).WithMessage($"Subject must not exceed {MaxSubjectLength} characters");

            RuleFor(x => x.Body!)
                .Must(body => System.Text.Encoding.UTF8.GetByteCount(body) <= MaxBodyLengthBytes)
                .WithMessage("Body must not exceed 10 MB");
        });

        // ─── Attachments ──────────────────────────────────────────────────────────
        When(x => x.Attachments is { Count: > 0 }, () =>
        {
            RuleFor(x => x.Attachments!)
                .Must(a => a.Count <= opts.MaxAttachmentsPerEmail)
                .WithMessage($"Maximum {opts.MaxAttachmentsPerEmail} attachments are allowed per email");

            RuleForEach(x => x.Attachments!)
                .ChildRules(attachment =>
                {
                    attachment.RuleFor(a => a.FileName)
                        .NotEmpty().WithMessage("Attachment FileName cannot be empty");

                    attachment.RuleFor(a => a.ContentType)
                        .NotEmpty().WithMessage("Attachment ContentType cannot be empty");

                    attachment.RuleFor(a => a.ContentBase64)
                        .NotEmpty().WithMessage("Attachment ContentBase64 cannot be empty")
                        .Must(IsValidBase64).WithMessage("Attachment ContentBase64 must be a valid Base64 string")
                        .Must(b64 => IsWithinSizeLimit(b64, opts.MaxAttachmentSizeBytes))
                        .WithMessage($"Each attachment must not exceed {opts.MaxAttachmentSizeBytes / (1024 * 1024)} MB");
                });
        });
    }

    private static bool IsValidBase64(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try
        {
            Convert.FromBase64String(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsWithinSizeLimit(string base64, long maxBytes)
    {
        var approximateBytes = (long)(base64.Length * 0.75);
        return approximateBytes <= maxBytes;
    }
}
