using FluentValidation;
using MSEMC.Abstractions;
using MSEMC.Contracts.Requests;
using MSEMC.Contracts.Responses;
using MSEMC.Domain.Entities;

namespace MSEMC.Endpoints;

/// <summary>
/// Endpoints Minimal API para operações de mensagens.
/// Substitui o EmailController legado por uma abordagem mais idiomática do .NET 8.
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
            .WithDescription("Recebe um e-mail e o enfileira para entrega assíncrona via broker de mensagens.")
            .Produces<SendEmailResponse>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);
    }

    private static async Task<IResult> SendMessage(
        SendEmailRequest request,
        IValidator<SendEmailRequest> validator,
        IEmailQueuePublisher publisher,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
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

        var message = EmailMessage.Create(
            recipient: request.Recipient,
            subject: request.Subject,
            body: request.Body,
            isHtml: request.IsHtml,
            ccRecipients: request.CcRecipients,
            bccRecipients: request.BccRecipients);

        logger.LogInformation(
            "Accepted email request for {Recipient} (MessageId: {MessageId})",
            request.Recipient, message.Id);

        await publisher.PublishAsync(message, cancellationToken);

        var response = new SendEmailResponse(
            MessageId: message.Id,
            Status: message.Status.ToString(),
            AcceptedAt: message.CreatedAt);

        return Results.Accepted($"/api/messages/{message.Id}", response);
    }
}
