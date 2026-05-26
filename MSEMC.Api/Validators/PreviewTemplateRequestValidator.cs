using FluentValidation;
using MSEMC.Contracts.Requests;

namespace MSEMC.Validators;

/// <summary>
/// Valida o PreviewTemplateRequest — garante que data é um JSON object válido.
/// </summary>
public sealed class PreviewTemplateRequestValidator : AbstractValidator<PreviewTemplateRequest>
{
    public PreviewTemplateRequestValidator()
    {
        RuleFor(x => x.Data)
            .Must(data => data.ValueKind == System.Text.Json.JsonValueKind.Object)
            .WithMessage("'data' must be a valid JSON object");

        RuleFor(x => x.SubjectOverride)
            .MaximumLength(998)
            .WithMessage("SubjectOverride must not exceed 998 characters")
            .When(x => x.SubjectOverride is not null);
    }
}
