using System.Net;
using System.Net.Mail;
using MSEMC.Models;

namespace MSEMC.Services
{
    public class EmailService
    {
        public void SendEmail(RequisicaoEmail requisicao) 
        {
            var smtpClient = new SmtpClient("smtp.gmail.com")
            {
                Port = 587,
                Credentials = new NetworkCredential("matheusmeigre@gmail.com", "__SENHA_DO_APP__"),
                EnableSsl = true,
            };

            var mensagemParaEmail = new MailMessage
            {
                From = new MailAddress("matheusmeigre@gmail.com"),
                Subject = requisicao.Assunto,
                Body = requisicao.Conteudo,
                IsBodyHtml = true
            };

            mensagemParaEmail.To.Add(requisicao.Destinatario);
            smtpClient.Send(mensagemParaEmail);
        }
    }
}
