using FluentValidation;
using MSEMC.Abstractions;
using MSEMC.Contracts.Requests;
using MSEMC.Contracts.Responses;

namespace MSEMC.Endpoints;

/// <summary>
/// Endpoints de gerenciamento e preview de templates.
/// GET  /api/templates              → catálogo de descoberta (todos os templates)
/// POST /api/templates/preview/{id} → preview do HTML renderizado (sem enviar e-mail)
/// </summary>
public static class TemplateEndpoints
{
    public static void MapTemplateEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/templates")
            .WithTags("Templates")
            .RequireAuthorization();

        // ── GET /api/templates ────────────────────────────────────────────────
        group.MapGet("/", ListTemplates)
            .WithName("ListTemplates")
            .WithSummary("Listar todos os templates disponíveis")
            .WithDescription(
                "Retorna o catálogo completo de templates cadastrados com:\n" +
                "- **templateId**: identificador para usar no preview e no envio\n" +
                "- **requiredVariables**: campos obrigatórios no payload `data`\n" +
                "- **optionalVariables**: campos opcionais no payload `data`\n" +
                "- **examplePayload**: exemplo de `data` pronto para copiar no preview\n" +
                "- **previewEndpoint**: endpoint direto para chamar o preview\n\n" +
                "Use o filtro `?domain=autenticacao` para listar templates de um domínio específico.")
            .Produces<ListTemplatesResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // ── POST /api/templates/preview/{**templateId} ────────────────────────
        // Rota: catch-all deve ser o último segmento
        // Exemplo: POST /api/templates/preview/autenticacao/codigo-seguranca
        group.MapPost("/preview/{**templateId}", PreviewTemplate)
            .WithName("PreviewTemplate")
            .WithSummary("Pré-visualizar um template renderizado")
            .WithDescription(
                "Renderiza o template com os dados fornecidos e retorna o HTML gerado. " +
                "Não envia e-mail. Use o HTML retornado em um `<iframe>` para preview.\n\n" +
                "**Dica**: chame `GET /api/templates` primeiro para obter o templateId e o examplePayload.")
            .Produces<PreviewTemplateResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }

    // ── Handlers ──────────────────────────────────────────────────────────────

    private static async Task<IResult> ListTemplates(
        ITemplateLoader loader,
        ILogger<Program> logger,
        string? domain = null,
        string? locale = null,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "Listando catálogo de templates (Locale: {Locale}, Domain: {Domain})",
            locale ?? "default", domain ?? "*");

        var listResult = await loader.ListAsync(locale, domain, cancellationToken);

        if (!listResult.IsSuccess)
        {
            return Results.Problem(
                detail: listResult.Error,
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Failed to list templates");
        }

        var baseUrl = "/api/templates/preview";

        var entries = listResult.Value!
            .Select(t => new TemplateEntry(
                TemplateId: t.TemplateId,
                Name: t.Name,
                Description: t.Description,
                Domain: t.Domain,
                SubjectTemplate: t.SubjectTemplate,
                RequiredVariables: t.RequiredVariables,
                OptionalVariables: t.OptionalVariables,
                ExamplePayload: t.ExamplePayload,
                PreviewEndpoint: $"POST {baseUrl}/{t.TemplateId}"))
            .ToList();

        var response = new ListTemplatesResponse(
            Templates: entries,
            Total: entries.Count);

        return Results.Ok(response);
    }

    private static async Task<IResult> PreviewTemplate(
        string templateId,
        PreviewTemplateRequest request,
        IValidator<PreviewTemplateRequest> validator,
        ITemplateRenderingService renderingService,
        ILogger<Program> logger,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // Normaliza URL-encoding: Swagger/clientes codificam '/' como '%2F'
        // Uri.UnescapeDataString converte 'autenticacao%2Fcodigo-seguranca' → 'autenticacao/codigo-seguranca'
        templateId = Uri.UnescapeDataString(templateId);

        // Fallback: aceita templateId via query string (?templateId=ecommerce/confirmacao-pedido)
        // O catch-all {**templateId} só vincula do path — clientes que usam query param recebem string vazia.
        if (string.IsNullOrWhiteSpace(templateId))
        {
            templateId = httpContext.Request.Query["templateId"].FirstOrDefault() ?? string.Empty;
            templateId = Uri.UnescapeDataString(templateId);
        }

        // Validação de segurança adicional no templateId (path traversal)
        if (templateId.Contains("..") || templateId.Contains('\\'))
        {
            return Results.Problem(
                detail: "Invalid templateId: path traversal is not allowed.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid Template ID");
        }

        // Garante que templateId não está vazio após todos os fallbacks
        if (string.IsNullOrWhiteSpace(templateId))
        {
            return Results.Problem(
                detail: "O templateId é obrigatório. Informe-o no path (POST /api/templates/preview/ecommerce/confirmacao-pedido) " +
                        "ou via query string (?templateId=ecommerce/confirmacao-pedido).",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Template ID ausente");
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
