namespace MSEMC.Contracts.Requests;

/// <summary>
/// Contrato de requisição para envio de uma mensagem de e-mail.
/// Record imutável utilizado como DTO do corpo da requisição HTTP.
/// </summary>
public sealed record SendEmailRequest(
    string Recipient,
    string Subject,
    string Body,
    bool IsHtml = true,
    List<string>? CcRecipients = null,
    List<string>? BccRecipients = null
);
