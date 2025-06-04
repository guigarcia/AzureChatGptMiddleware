using Microsoft.AspNetCore.Mvc;
using AzureChatGptMiddleware.Models;
using AzureChatGptMiddleware.Services;
using Microsoft.AspNetCore.Http; // Necessário para StatusCodes
using Microsoft.Extensions.Configuration;

namespace AzureChatGptMiddleware.Controllers
{
    /// <summary>
    /// Controller responsável pela autenticação e geração de tokens JWT.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly ILogger<AuthController> _logger;
        private readonly IConfiguration _configuration;

        public AuthController(ITokenService tokenService, ILogger<AuthController> logger, IConfiguration configuration)
        {
            _tokenService = tokenService;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Gera um token JWT para autenticação na API mediante uma API Key válida.
        /// </summary>
        /// <remarks>
        /// Forneça uma API Key válida no corpo da requisição para obter um token JWT.
        /// Este token JWT deve ser incluído no header 'Authorization' como 'Bearer [token]'
        /// para acessar os endpoints protegidos da API.
        /// </remarks>
        /// <param name="request">Objeto contendo a API Key do cliente.</param>
        /// <returns>Um token JWT válido e sua data de expiração se a API Key for correta.</returns>
        /// <response code="200">Sucesso. Retorna o token JWT e sua data de expiração.</response>
        /// <response code="400">Requisição inválida. Ocorre se o corpo da requisição ou a API Key não forem fornecidos. (Validação de modelo via FluentValidation não está configurada para TokenRequest especificamente, mas seria aplicável se estivesse).</response>
        /// <response code="401">Não autorizado. Ocorre se a API Key fornecida for inválida.</response>
        /// <response code="500">Erro interno do servidor. Pode ocorrer se houver um problema na configuração do JWT (ex: SecretKey ausente no TokenService) ou outra falha inesperada.</response>
        [HttpPost("token")]
        [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status500InternalServerError)]
        public IActionResult GenerateToken([FromBody] TokenRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.ApiKey))
            {
                // Adicionando log para requisição inválida
                _logger.LogWarning("Tentativa de gerar token com requisição inválida (nula ou API Key ausente).");
                return BadRequest(new ErrorResponseModel { Message = "API Key é obrigatória na requisição." });
            }

            try
            {
                if (!_tokenService.ValidateApiKey(request.ApiKey))
                {
                    _logger.LogWarning("Tentativa de autenticação com API Key inválida: {ApiKey}", request.ApiKey);
                    return Unauthorized(new ErrorResponseModel { Message = "API Key inválida." });
                }

                var token = _tokenService.GenerateToken();
                var expirationMinutesValue = _configuration["JwtSettings:ExpirationMinutes"];
                var expirationMinutes = string.IsNullOrWhiteSpace(expirationMinutesValue) ? 60 : Convert.ToDouble(expirationMinutesValue);
                var expiresAt = DateTime.UtcNow.AddMinutes(expirationMinutes);

                _logger.LogInformation("Token JWT gerado com sucesso via API Key.");

                return Ok(new TokenResponse
                {
                    Token = token,
                    ExpiresAt = expiresAt
                });
            }
            catch (InvalidOperationException ex) // Ex: SecretKey JWT não configurada no TokenService
            {
                _logger.LogError(ex, "Erro de configuração ao tentar gerar token JWT: {ErrorMessage}", ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseModel { Message = $"Erro interno de configuração ao gerar token: {ex.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao gerar token via API Key.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseModel { Message = "Erro inesperado ao processar a solicitação do token. Tente novamente mais tarde." });
            }
        }
    }

    /// <summary>
    /// Modelo de resposta para erros padronizados na API.
    /// </summary>
    public class ErrorResponseModel
    {
        /// <summary>
        /// Mensagem descritiva do erro ocorrido.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}