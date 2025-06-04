using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AzureChatGptMiddleware.Models; // Assegura que ErrorResponseModel está acessível se definido aqui ou em Models
using AzureChatGptMiddleware.Services;
using Microsoft.AspNetCore.Http; // Necessário para StatusCodes

namespace AzureChatGptMiddleware.Controllers
{
    /// <summary>
    /// Controller para gerenciar os prompts do sistema utilizados pela IA.
    /// Requer autenticação (JWT ou API Key).
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Protege todos os endpoints deste controller
    [Produces("application/json")]
    public class PromptController : ControllerBase
    {
        private readonly IPromptService _promptService;
        private readonly ILogger<PromptController> _logger;

        public PromptController(IPromptService promptService, ILogger<PromptController> logger)
        {
            _promptService = promptService;
            _logger = logger;
        }

        /// <summary>
        /// Lista todos os prompts do sistema cadastrados.
        /// </summary>
        /// <returns>Uma lista de prompts.</returns>
        /// <response code="200">Retorna a lista de prompts.</response>
        /// <response code="500">Erro interno do servidor ao tentar buscar os prompts.</response>
        [HttpGet]
        [ProducesResponseType(typeof(List<PromptResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var prompts = await _promptService.GetAllPromptsAsync();
                return Ok(prompts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao listar todos os prompts.");
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseModel { Message = "Erro interno ao processar a requisição para listar prompts." });
            }
        }

        /// <summary>
        /// Busca um prompt específico pelo seu ID.
        /// </summary>
        /// <param name="id">O ID do prompt a ser buscado.</param>
        /// <returns>Os dados do prompt encontrado.</returns>
        /// <response code="200">Retorna os dados do prompt.</response>
        /// <response code="404">Se o prompt com o ID especificado não for encontrado.</response>
        /// <response code="500">Erro interno do servidor.</response>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PromptResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var prompt = await _promptService.GetPromptByIdAsync(id);
                if (prompt == null)
                {
                    _logger.LogInformation("Prompt com ID {PromptId} não encontrado.", id);
                    return NotFound(new ErrorResponseModel { Message = $"Prompt com ID {id} não encontrado." });
                }
                return Ok(prompt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar prompt com ID {PromptId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseModel { Message = "Erro interno ao processar a requisição para buscar o prompt." });
            }
        }

        /// <summary>
        /// Cria um novo prompt do sistema.
        /// </summary>
        /// <param name="request">Os dados do prompt a ser criado. Validações (Nome/Conteúdo obrigatórios, limites de tamanho) são aplicadas via FluentValidation.</param>
        /// <returns>Os dados do prompt recém-criado.</returns>
        /// <response code="201">Retorna o prompt recém-criado com a URL para acessá-lo.</response>
        /// <response code="400">Requisição inválida. Ocorre se os dados do prompt não atenderem aos critérios de validação (ex: campos obrigatórios ausentes, tamanho excedido - via FluentValidation) ou se já existir um prompt com o mesmo nome.</response>
        /// <response code="500">Erro interno do servidor.</response>
        [HttpPost]
        [ProducesResponseType(typeof(PromptResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status400BadRequest)] 
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Create([FromBody] PromptRequest request)
        {
            try
            {
                var createdPromptEntity = await _promptService.CreatePromptAsync(request);
                _logger.LogInformation("Prompt '{PromptName}' (ID: {PromptId}) criado com sucesso.", createdPromptEntity.Name, createdPromptEntity.Id);

                var responseDto = new PromptResponse
                {
                    Id = createdPromptEntity.Id,
                    Name = createdPromptEntity.Name,
                    Content = createdPromptEntity.Content,
                    IsActive = createdPromptEntity.IsActive,
                    CreatedAt = createdPromptEntity.CreatedAt,
                    UpdatedAt = createdPromptEntity.UpdatedAt
                };
                return CreatedAtAction(nameof(GetById), new { id = responseDto.Id }, responseDto);
            }
            catch (InvalidOperationException ex) 
            {
                _logger.LogWarning(ex, "Falha ao criar prompt devido a uma operação inválida: {ErrorMessage}", ex.Message);
                return BadRequest(new ErrorResponseModel { Message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao criar prompt com nome '{PromptName}'.", request.Name);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseModel { Message = "Erro interno ao processar a requisição para criar o prompt." });
            }
        }

        /// <summary>
        /// Atualiza um prompt do sistema existente.
        /// </summary>
        /// <param name="id">O ID do prompt a ser atualizado.</param>
        /// <param name="request">Os novos dados para o prompt. Validações são aplicadas via FluentValidation.</param>
        /// <returns>Os dados do prompt atualizado.</returns>
        /// <response code="200">Retorna o prompt atualizado.</response>
        /// <response code="400">Requisição inválida. Ocorre se os dados do prompt não atenderem aos critérios de validação (via FluentValidation).</response>
        /// <response code="404">Se o prompt com o ID especificado não for encontrado.</response>
        /// <response code="500">Erro interno do servidor.</response>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(PromptResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status400BadRequest)] // Também pode ser ValidationProblemDetails
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ErrorResponseModel), StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> Update(int id, [FromBody] PromptRequest request)
        {
            try
            {
                var updatedPromptEntity = await _promptService.UpdatePromptAsync(id, request);
                _logger.LogInformation("Prompt ID {PromptId} ('{PromptName}') atualizado com sucesso.", updatedPromptEntity.Id, updatedPromptEntity.Name);
                
                var responseDto = new PromptResponse 
                {
                    Id = updatedPromptEntity.Id,
                    Name = updatedPromptEntity.Name,
                    Content = updatedPromptEntity.Content,
                    IsActive = updatedPromptEntity.IsActive,
                    CreatedAt = updatedPromptEntity.CreatedAt,
                    UpdatedAt = updatedPromptEntity.UpdatedAt
                };
                return Ok(responseDto);
            }
            catch (InvalidOperationException ex) 
            {
                _logger.LogWarning(ex, "Falha ao atualizar prompt ID {PromptId} devido a uma operação inválida (ex: não encontrado): {ErrorMessage}", id, ex.Message);
                return NotFound(new ErrorResponseModel { Message = ex.Message }); 
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro inesperado ao atualizar prompt ID {PromptId}.", id);
                return StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponseModel { Message = "Erro interno ao processar a requisição para atualizar o prompt." });
            }
        }
    }
}
