using FluentValidation;
using MSEMC.Abstractions;
using MSEMC.Contracts.Requests;
using MSEMC.Contracts.Responses;
using MSEMC.Domain.Entities;

namespace MSEMC.Endpoints;

/// <summary>
/// Endpoints Minimal API para operações de mensagens.
/// Suporta dois modos: Template (novo) e Raw (legado).
/// </summary>
public static class MessageEndpoints
{
    public static void MapMessageEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/messages")
            .WithTags("Mensagens")
            .RequireAuthorization()
            .RequireRateLimiting("messages");

        group.MapPost("/", SendMessage)
            .WithName("SendMessage")
            .WithSummary("Enfileirar uma mensagem de e-mail para envio")
            .WithDescription(
                "Suporta dois modos mutuamente exclusivos:\n" +
                "- **Template Mode**: forneça `templateId` + `data`. O backend renderiza o HTML via Scriban.\n" +
                "- **Raw Mode (legado)**: forneça `subject` + `body` diretamente.")
            .Produces<SendEmailResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> SendMessage(
        SendEmailRequest request,
        IValidator<SendEmailRequest> validator,
        IEmailQueuePublisher publisher,
        ITemplateRenderingService renderingService,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        // ── Validação de contrato (FluentValidation) ──────────────────────────────
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

        // ── Resolução de body + subject ───────────────────────────────────────────
        string body;
        string subject;

        if (request.TemplateId is not null)
        {
            // MODO TEMPLATE: renderizar via pipeline antes de enfileirar
            var renderResult = await renderingService.RenderAsync(
                templateId: request.TemplateId,
                locale: request.Locale,
                data: request.Data!.Value,
                subjectOverride: request.Subject,
                cancellationToken: cancellationToken);

            if (!renderResult.IsSuccess)
            {
                // Determina se é 400 (template não encontrado / dados inválidos) ou 422
                return Results.Problem(
                    detail: renderResult.Error,
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Template rendering failed");
            }

            body = renderResult.Value!.RenderedHtml;
            subject = renderResult.Value.ResolvedSubject;
        }
        else
        {
            // MODO RAW: passa body e subject diretamente
            body = request.Body!;
            subject = request.Subject!;
        }

        // ── Criar entidade de domínio ─────────────────────────────────────────────
        var message = EmailMessage.Create(
            recipient: request.Recipient,
            subject: subject,
            body: body,
            isHtml: request.IsHtml,
            ccRecipients: request.CcRecipients,
            bccRecipients: request.BccRecipients,
            attachments: request.Attachments);

        logger.LogInformation(
            "Solicitação de e-mail aceita para {Recipient} (MessageId: {MessageId}, Mode: {Mode})",
            request.Recipient, message.Id, request.TemplateId is not null ? "Template" : "Raw");

        // ── Enfileirar para envio assíncrono ─────────────────────────────────────
        await publisher.PublishAsync(message, cancellationToken);

        var response = new SendEmailResponse(
            MessageId: message.Id,
            Status: message.Status.ToString(),
            AcceptedAt: message.CreatedAt);

        return Results.Accepted($"/api/messages/{message.Id}", response);
    }
}
