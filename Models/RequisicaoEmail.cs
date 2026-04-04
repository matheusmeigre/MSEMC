using System.ComponentModel.DataAnnotations;

namespace MSEMC.Models
{
    public class RequisicaoEmail
    {
        [Required]
        [EmailAddress]
        public string Destinatario { get; set; }

        [Required]
        public string Assunto { get; set; }

        [Required]
        public string Conteudo { get; set; }

        public List<string> Destinatarios { get; set; }
        public List<string>? Cc { get; set; }
        public List<string>? Bcc { get; set; }

        public List<Object>? Attachments { get; set; }
    }
}
