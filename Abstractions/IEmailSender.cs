using MSEMC.Domain.Entities;
using MSEMC.Domain.Results;

namespace MSEMC.Abstractions;

/// <summary>
/// Abstração para envio de mensagens de e-mail.
/// Aplica Inversão de Dependência — os controllers dependem desta interface,
/// não de implementações concretas (SmtpClient, MailKit, etc.).
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// Envia uma mensagem de e-mail de forma assíncrona.
    /// Retorna um Result indicando sucesso ou falha sem lançar exceções.
    /// </summary>
    /// <param name="message">A mensagem de e-mail a ser enviada.</param>
    /// <param name="cancellationToken">Token para cancelar a operação.</param>
    /// <returns>Um Result contendo o EmailMessage atualizado em caso de sucesso, ou um erro em caso de falha.</returns>
    Task<Result<EmailMessage>> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
