using System.Linq.Expressions;
using System.Net;
using System.Net.Mail;
using MSEMC.Models;

namespace MSEMC.Services
{
    public class EmailService
    {
        private readonly ILogger<EmailService> _logger;
        public EmailService(ILogger<EmailService> logger)
        {
            _logger = logger;
        }

        public void EnviarEmail(RequisicaoEmail requisicao) 
        {
            var emailApp = _config["ConfiguracaoEmail:Email"];
            var senhaApp = _config["ConfiguracaoEmail:Senha"];


            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential(emailApp, senhaApp),
                EnableSsl = true,
            };

            var mensagemParaEmail = new MailMessage
            {
                From = new MailAddress(emailApp),
                Subject = requisicao.Assunto,
                Body = requisicao.Conteudo,
                IsBodyHtml = true
            };

            mensagemParaEmail.To.Add(requisicao.Destinatario);
            _logger.LogInformation("Atribuindo mensagemParaEmail ao Destinatario");

            bool sendMessage = false;
            try
            {
                if (sendMessage)
                {
                    smtpClient.Send(mensagemParaEmail);
                    sendMessage = true;
                    _logger.LogInformation($"Mensagem enviada com sucesso para: {requisicao.Destinatario} em: {DateTime.UtcNow}");
                }
                else 
                {
                    sendMessage = false;
                    _logger.LogInformation($"Mensagem não enviada para: {requisicao.Destinatario} houveram problemas em: {DateTime.UtcNow}");
                }
            }
            catch (Exception ex)
            {
                sendMessage = false;
                _logger.LogError($"Envio de mensagem não realizado: {ex.Message} - Data: {DateTime.UtcNow}");
            }
            }
        }
    }
