using System.Net;
using System.Net.Mail;
using MSEMC.Models;

namespace MSEMC.Services
{
    public class EmailService
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config) 
        {
            _config = config;
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
            smtpClient.Send(mensagemParaEmail);
        }
    }
}
