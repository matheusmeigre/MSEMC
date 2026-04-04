using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MSEMC.Models;
using MSEMC.Services;

namespace MSEMC.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly EmailService _emailService;

        public EmailController(ILogger<EmailService> logger)
        {
            _emailService = new EmailService(logger);
        }

        [HttpPost]
        public IActionResult EnviarEmail([FromBody] RequisicaoEmail requisicao) 
        {
            try 
            {
                _emailService.EnviarEmail(requisicao);
                return Ok("Email enviado com sucesso");
            } 
            catch (Exception exception)
            { 
                return BadRequest($"Erro: {exception.Message}");
            }
        }
    }
}
