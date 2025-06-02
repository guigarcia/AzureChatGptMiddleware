using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AzureChatGptMiddleware.Models; // Assegura que ErrorResponseModel está acessível
using AzureChatGptMiddleware.Services;
using AzureChatGptMiddleware.Exceptions; // Para AzureOpenAIComunicationException
using Microsoft.AspNetCore.Http; // Necessário para StatusCodes

namespace AzureChatGptMiddleware.Controllers
{
    /// <summary>
    /// Controller responsável pelo processamento de e-mails utilizando o serviço Azure OpenAI.
    /// Requer autenticação (JWT ou API Key).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Protege todos os endpoints deste controller
    [Produces("application/json")]
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
        /// Processa o conteúdo de um e-mail utilizando um prompt de sistema configurado e retorna a resposta gerada pela IA.
        /// </summary>
        /// <param name="request">Requisição contendo o conteúdo do e-mail a ser processado. Validações (conteúdo obrigatório, tamanho máximo) são aplicadas via FluentValidation.</param>
        /// <returns>A resposta processada pela IA, juntamente com informações de log.</returns>
        /// <response code="200">E-mail processado com sucesso. Retorna a resposta da IA.</response>
        /// <response code="400">Requisição inválida. Ocorre se o conteúdo do e-mail não atender aos critérios de validação (ex: ausente, tamanho excedido - via FluentValidation).</response>
        /// <response code="401">Não autorizado. Ocorre se o token JWT ou a API Key forem inválidos ou ausentes.</response>
        /// <response code="500">Erro interno do servidor. Pode ocorrer devido a falhas na comunicação com o Azure OpenAI Service, problemas no serviço de log, ou outras exceções inesperadas.</response>
        /// <response code="503">Serviço indisponível. Pode ser retornado especificamente se o Azure OpenAI Service estiver inacessível ou retornar um erro indicando indisponibilidade (requer tratamento da AzureOpenAIComunicationException).</response>
        [HttpPost("process")]
        [ProducesResponseType(typeof(EmailResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status400BadRequest)] // Também pode ser ValidationProblemDetails
        [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)] // Não retorna corpo para 401 do [Authorize]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status500InternalServerError)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> ProcessEmail([FromBody] EmailRequest request)
        {
            // A validação do modelo (ex: Message obrigatório) é esperada ser tratada
            // pelo pipeline do ASP.NET Core e FluentValidation.

            var clientInfo = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "IP Desconhecido";
            _logger.LogInformation("Iniciando processamento de e-mail para o cliente {ClientInfo}. Tamanho da mensagem: {MessageLength} caracteres.", clientInfo, request.Message?.Length ?? 0);

            try
            {
                var aiResponse = await _azureOpenAIService.ProcessEmailAsync(request.Message);

                // Registrar log de sucesso
                try
                {
                    var log = await _requestLogService.LogRequestAsync(
                        request.Message,
                        aiResponse,
                        true,
                        null, // Sem mensagem de erro
                        clientInfo);
                    _logger.LogInformation("E-mail processado com sucesso. RequestId: {RequestId}, ClientInfo: {ClientInfo}", log.Id, clientInfo);
                    return Ok(new EmailResponse
                    {
                        Response = aiResponse,
                        RequestId = log.Id,
                        ProcessedAt = log.CreatedAt
                    });
                }
                catch (Exception logEx)
                {
                    // Se o log falhar, o erro principal já ocorreu (OpenAI), ou este é um novo erro.
                    // Loga a falha no log e retorna o erro 500, pois o processamento principal (OpenAI) teve sucesso, mas o log não.
                    _logger.LogError(logEx, "Falha ao registrar log de sucesso para processamento de e-mail. ClientInfo: {ClientInfo}. Resposta da IA (omitida no log por segurança, mas foi): {AIReponseCharCount} chars.", clientInfo, aiResponse.Length);
                    // Decide-se retornar 500 pois uma parte crítica (auditoria/log) falhou, mesmo que a IA tenha respondido.
                    // Alternativamente, poderia retornar Ok e apenas logar o erro do log.
                    return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseModel { Message = "E-mail processado, mas falha ao registrar o log da requisição." });
                }
            }
            catch (AzureOpenAIComunicationException openAIEx)
            {
                _logger.LogError(openAIEx, "Erro de comunicação com Azure OpenAI Service ao processar e-mail. ClientInfo: {ClientInfo}. Status Code: {StatusCode}. ErrorContent: {ErrorContent}", clientInfo, openAIEx.StatusCode, openAIEx.ErrorResponseContent);
                await TryLogErrorToDatabase(request.Message, $"AzureOpenAIComunicationException: {openAIEx.Message} (StatusCode: {openAIEx.StatusCode})", clientInfo);

                // Retorna 503 se for um erro de comunicação/serviço claro, senão 500.
                if (openAIEx.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                    openAIEx.StatusCode == System.Net.HttpStatusCode.GatewayTimeout ||
                    openAIEx.InnerException is HttpRequestException ||
                    openAIEx.InnerException is TaskCanceledException) // TaskCanceledException pode ser timeout
                {
                    return StatusCode(StatusCodes.Status503ServiceUnavailable, new ErrorResponseModel { Message = "O serviço do Azure OpenAI está temporariamente indisponível ou houve um problema de comunicação. Tente novamente mais tarde." });
                }
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseModel { Message = $"Erro ao comunicar com o serviço de IA: {openAIEx.Message}" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao processar e-mail. ClientInfo: {ClientInfo}.", clientInfo);
                await TryLogErrorToDatabase(request.Message, $"Exception: {ex.Message}", clientInfo);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseModel { Message = "Erro inesperado ao processar sua requisição. Tente novamente mais tarde." });
            }
        }

        private async Task TryLogErrorToDatabase(string originalRequest, string errorMessage, string? clientInfo)
        {
            try
            {
                await _requestLogService.LogRequestAsync(
                    originalRequest,
                    string.Empty, // Sem resposta da IA em caso de erro
                    false,        // Sucesso = false
                    errorMessage,
                    clientInfo);
            }
            catch (Exception dbLogEx)
            {
                // Se o log no banco de dados falhar também, apenas loga no sistema de log principal.
                _logger.LogError(dbLogEx, "Falha crítica: Erro ao tentar registrar falha de processamento de e-mail no banco de dados. Mensagem original do erro: {OriginalErrorMessage}", errorMessage);
            }
        }
    }
}
