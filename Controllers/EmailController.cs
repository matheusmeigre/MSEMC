using Microsoft.AspNetCore.Mvc;
using MSEMC.Models;
using MSEMC.Services;
using Microsoft.Extensions.Configuration;

namespace MSEMC.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly EmailService _emailService;

        public EmailController(IConfiguration config)
        {
            _emailService = new EmailService(config);
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
