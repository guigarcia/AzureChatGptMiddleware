using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AzureChatGptMiddleware.Models;
using AzureChatGptMiddleware.Services;

namespace AzureChatGptMiddleware.Controllers
{
    /// <summary>
    /// Controller responsável pelo gerenciamento de prompts do sistema
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
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
        /// Lista todos os prompts cadastrados
        /// </summary>
        /// <returns>Lista de prompts</returns>
        [HttpGet]
        [ProducesResponseType(typeof(List<PromptResponse>), 200)]
        public async Task<IActionResult> GetAll()
        {
            try
            {
                var prompts = await _promptService.GetAllPromptsAsync();
                return Ok(prompts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao listar prompts");
                return StatusCode(500, new { message = "Erro ao processar requisição" });
            }
        }

        /// <summary>
        /// Busca um prompt específico pelo ID
        /// </summary>
        /// <param name="id">ID do prompt</param>
        /// <returns>Dados do prompt</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(PromptResponse), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetById(int id)
        {
            try
            {
                var prompt = await _promptService.GetPromptByIdAsync(id);
                if (prompt == null)
                {
                    return NotFound(new { message = "Prompt não encontrado" });
                }
                return Ok(prompt);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao buscar prompt");
                return StatusCode(500, new { message = "Erro ao processar requisição" });
            }
        }

        /// <summary>
        /// Cria um novo prompt
        /// </summary>
        /// <param name="request">Dados do prompt</param>
        /// <returns>Prompt criado</returns>
        [HttpPost]
        [ProducesResponseType(typeof(PromptResponse), 201)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> Create([FromBody] PromptRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Content))
                {
                    return BadRequest(new { message = "Nome e conteúdo são obrigatórios" });
                }

                var prompt = await _promptService.CreatePromptAsync(request);

                _logger.LogInformation($"Prompt '{prompt.Name}' criado com sucesso");

                return CreatedAtAction(nameof(GetById), new { id = prompt.Id }, new PromptResponse
                {
                    Id = prompt.Id,
                    Name = prompt.Name,
                    Content = prompt.Content,
                    IsActive = prompt.IsActive,
                    CreatedAt = prompt.CreatedAt
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar prompt");
                return StatusCode(500, new { message = "Erro ao processar requisição" });
            }
        }

        /// <summary>
        /// Atualiza um prompt existente
        /// </summary>
        /// <param name="id">ID do prompt</param>
        /// <param name="request">Novos dados do prompt</param>
        /// <returns>Prompt atualizado</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(typeof(PromptResponse), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Update(int id, [FromBody] PromptRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Content))
                {
                    return BadRequest(new { message = "Nome e conteúdo são obrigatórios" });
                }

                var prompt = await _promptService.UpdatePromptAsync(id, request);

                _logger.LogInformation($"Prompt '{prompt.Name}' atualizado com sucesso");

                return Ok(new PromptResponse
                {
                    Id = prompt.Id,
                    Name = prompt.Name,
                    Content = prompt.Content,
                    IsActive = prompt.IsActive,
                    CreatedAt = prompt.CreatedAt,
                    UpdatedAt = prompt.UpdatedAt
                });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao atualizar prompt");
                return StatusCode(500, new { message = "Erro ao processar requisição" });
            }
        }
    }
}
