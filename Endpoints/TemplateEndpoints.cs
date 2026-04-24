using FluentValidation;
using MSEMC.Abstractions;
using MSEMC.Contracts.Requests;
using MSEMC.Contracts.Responses;

namespace MSEMC.Endpoints;

/// <summary>
/// Endpoints de preview de templates — permite visualizar o HTML renderizado
/// sem enviar o e-mail. Destinado a painéis administrativos.
/// </summary>
public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/templates")
            .WithTags("Templates")
            .RequireAuthorization();

        // Rota: /preview/{**templateId} — catch-all deve ser o último segmento
        // Exemplo: POST /api/templates/preview/autenticacao/codigo-seguranca
        group.MapPost("/preview/{**templateId}", PreviewTemplate)
            .WithName("PreviewTemplate")
            .WithSummary("Pré-visualizar um template renderizado")
            .WithDescription(
                "Renderiza o template com os dados fornecidos e retorna o HTML gerado. " +
                "Não envia e-mail. Use o HTML retornado em um `<iframe>` para preview.")
            .Produces<PreviewTemplateResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    private static async Task<IResult> PreviewTemplate(
        string templateId,
        PreviewTemplateRequest request,
        IValidator<PreviewTemplateRequest> validator,
        ITemplateRenderingService renderingService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // Validação de segurança adicional no templateId (path traversal)
        if (templateId.Contains("..") || templateId.Contains('\\'))
        {
            return Results.Problem(
                detail: "Invalid templateId: path traversal is not allowed.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Template ID");
        }

        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(
                validationResult.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()));
        }

        logger.LogInformation(
            "Solicitação de preview do template '{TemplateId}' (Locale: {Locale})",
            templateId, request.Locale ?? "default");

        var renderResult = await renderingService.RenderAsync(
            templateId: templateId,
            locale: request.Locale,
            data: request.Data,
            subjectOverride: request.SubjectOverride,
            cancellationToken: cancellationToken);

        if (!renderResult.IsSuccess)
        {
            return Results.Problem(
                detail: renderResult.Error,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Template rendering failed");
        }

        var result = renderResult.Value!;

        var response = new PreviewTemplateResponse(
            TemplateId: templateId,
            Locale: result.ResolvedLocale,
            Subject: result.ResolvedSubject,
            RenderedHtml: result.RenderedHtml,
            RenderedAt: DateTimeOffset.UtcNow);

        return Results.Ok(response);
    }
}
