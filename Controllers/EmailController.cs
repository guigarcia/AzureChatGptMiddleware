using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AzureChatGptMiddleware.Models;
using AzureChatGptMiddleware.Services;

namespace AzureChatGptMiddleware.Controllers
{
    /// <summary>
    /// Controller responsável pelo processamento de e-mails via Azure ChatGPT
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class EmailController : ControllerBase
    {
        private readonly IAzureOpenAIService _azureOpenAIService;
        private readonly IRequestLogService _requestLogService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(
            IAzureOpenAIService azureOpenAIService,
            IRequestLogService requestLogService,
            ILogger<EmailController> logger)
        {
            _azureOpenAIService = azureOpenAIService;
            _requestLogService = requestLogService;
            _logger = logger;
        }

        /// <summary>
        /// Processa um e-mail aplicando o prompt configurado e retorna a resposta da IA
        /// </summary>
        /// <param name="request">Requisição contendo o conteúdo do e-mail</param>
        /// <returns>Resposta processada pela IA</returns>
        /// <response code="200">E-mail processado com sucesso</response>
        /// <response code="400">Requisição inválida</response>
        /// <response code="401">Não autorizado - Token JWT ausente ou inválido</response>
        /// <response code="500">Erro interno do servidor</response>
        [HttpPost("process")]
        [ProducesResponseType(typeof(EmailResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        [ProducesResponseType(500)]
        public async Task<IActionResult> ProcessEmail([FromBody] EmailRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Conteúdo do e-mail não pode estar vazio" });
            }

            try
            {
                _logger.LogInformation("Iniciando processamento de e-mail");

                // Processar com Azure OpenAI
                var aiResponse = await _azureOpenAIService.ProcessEmailAsync(request.Message);

                // Registrar no banco de dados
                var clientInfo = HttpContext.Connection.RemoteIpAddress?.ToString();
                var log = await _requestLogService.LogRequestAsync(
                    request.Message,
                    aiResponse,
                    true,
                    null,
                    clientInfo);

                _logger.LogInformation($"E-mail processado com sucesso. RequestId: {log.Id}");

                return Ok(new EmailResponse
                {
                    Response = aiResponse,
                    RequestId = log.Id,
                    ProcessedAt = log.CreatedAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar e-mail");

                // Registrar erro no banco
                var clientInfo = HttpContext.Connection.RemoteIpAddress?.ToString();
                await _requestLogService.LogRequestAsync(
                    request.Message,
                    string.Empty,
                    false,
                    ex.Message,
                    clientInfo);

                return StatusCode(500, new { message = "Erro ao processar requisição. Tente novamente mais tarde." });
            }
        }
    }
}
