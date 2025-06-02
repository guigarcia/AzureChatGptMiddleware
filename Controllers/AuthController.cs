using Microsoft.AspNetCore.Mvc;
using AzureChatGptMiddleware.Models;
using AzureChatGptMiddleware.Services;

namespace AzureChatGptMiddleware.Controllers
{
    /// <summary>
    /// Controller responsável pela autenticação e geração de tokens
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(ITokenService tokenService, ILogger<AuthController> logger)
        {
            _tokenService = tokenService;
            _logger = logger;
        }

        /// <summary>
        /// Gera um token JWT válido mediante apresentação de API Key válida
        /// </summary>
        /// <param name="request">Requisição contendo a API Key</param>
        /// <returns>Token JWT e data de expiração</returns>
        /// <response code="200">Token gerado com sucesso</response>
        /// <response code="401">API Key inválida</response>
        [HttpPost("token")]
        [ProducesResponseType(typeof(TokenResponse), 200)]
        [ProducesResponseType(401)]
        public IActionResult GenerateToken([FromBody] TokenRequest request)
        {
            try
            {
                if (!_tokenService.ValidateApiKey(request.ApiKey))
                {
                    _logger.LogWarning($"Tentativa de autenticação com API Key inválida");
                    return Unauthorized(new { message = "API Key inválida" });
                }

                var token = _tokenService.GenerateToken();
                var expiresAt = DateTime.UtcNow.AddMinutes(60); // Configurável via appsettings

                _logger.LogInformation("Token JWT gerado com sucesso");

                return Ok(new TokenResponse
                {
                    Token = token,
                    ExpiresAt = expiresAt
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar token");
                return StatusCode(500, new { message = "Erro ao processar requisição" });
            }
        }
    }
}