using Microsoft.EntityFrameworkCore;
using AzureChatGptMiddleware.Data;
using AzureChatGptMiddleware.Data.Entities;
using AzureChatGptMiddleware.Models;

namespace AzureChatGptMiddleware.Services
{
    public class PromptService : IPromptService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PromptService> _logger;

        private const string DefaultPromptContent = @"Escreva o e-mail de resposta ao cliente para um produto de internet via satélite, garantindo que a comunicação seja
acolhedora, clara e assertiva. O tom deve transmitir empatia e profissionalismo, proporcionando uma experiência positiva ao
cliente. O e-mail sempre deve estar em primeira pessoa, em que a resposta seja sempre entendida que o atendente que o
responde está disposto a ajudar. Não deixe de inserir a assinatura padrão da empresa Hughes conforme formato esperado
 
Diretrizes para a resposta:
Clareza e Objetividade: A mensagem deve ser compreensível e direta, evitando termos técnicos desnecessários.
Tom Acolhedor: Utilize uma linguagem amigável e cortês, demonstrando empatia com a solicitação do cliente.
Assertividade: Forneça informações precisas e diretas, evitando ambiguidades.
Reforço Positivo: Sempre que possível, reforce a preocupação da empresa com a satisfação do cliente.
Acolhimento: para os casos de e-mail que conste as palavras 'atraso', 'não comparecimento do técnico', 'problemas'
Fechamento Construtivo: Finalize a mensagem oferecendo suporte adicional e informando próximos passos, se aplicável.
 
Formato esperado da resposta:
Manter a resposta em primeira pessoa
Reconhecimento da solicitação do cliente
Explicação clara e objetiva da resposta
Solução ou direcionamento adequado
Fechamento cordial e oferta de suporte adicional
Não utilizar ícones ou emojis nos tópicos do e-mail.
Ao final de todo e-mail, inserir a assinatura padrão da Hughes neste formato: 'Nossos canais de atendimento são:
www.hughesnet.com.br
atendimento@hughes.net.br
SAC: 0800 889 4000 (de segunda a sábado - 8h00 às 20h00)
Conheça o nosso WhatsApp, telefone (11)3956-3931. Lá você consegue obter a 2ª via da fatura, consultar seu pacote de
dados e muito mais!'";

        public PromptService(ApplicationDbContext context, ILogger<PromptService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<string> GetActivePromptContentAsync(string name)
        {
            var prompt = await _context.Prompts
                .Where(p => p.Name == name && p.IsActive)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt)
                .FirstOrDefaultAsync();

            if (prompt == null)
            {
                _logger.LogWarning($"Prompt '{name}' não encontrado. Usando prompt padrão.");
                return DefaultPromptContent;
            }

            return prompt.Content;
        }

        public async Task<Prompt> CreatePromptAsync(PromptRequest request)
        {
            var existingPrompt = await _context.Prompts
                .FirstOrDefaultAsync(p => p.Name == request.Name);

            if (existingPrompt != null)
            {
                throw new InvalidOperationException($"Já existe um prompt com o nome '{request.Name}'");
            }

            var prompt = new Prompt
            {
                Name = request.Name,
                Content = request.Content,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            _context.Prompts.Add(prompt);
            await _context.SaveChangesAsync();

            return prompt;
        }

        public async Task<Prompt> UpdatePromptAsync(int id, PromptRequest request)
        {
            var prompt = await _context.Prompts.FindAsync(id);
            if (prompt == null)
            {
                throw new InvalidOperationException($"Prompt com ID {id} não encontrado");
            }

            prompt.Name = request.Name;
            prompt.Content = request.Content;
            prompt.IsActive = request.IsActive;
            prompt.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return prompt;
        }

        public async Task<List<PromptResponse>> GetAllPromptsAsync()
        {
            return await _context.Prompts
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PromptResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Content = p.Content,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<PromptResponse?> GetPromptByIdAsync(int id)
        {
            return await _context.Prompts
                .Where(p => p.Id == id)
                .Select(p => new PromptResponse
                {
                    Id = p.Id,
                    Name = p.Name,
                    Content = p.Content,
                    IsActive = p.IsActive,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task EnsureDefaultPromptAsync()
        {
            var existingPrompt = await _context.Prompts
                .FirstOrDefaultAsync(p => p.Name == "email_response");

            if (existingPrompt == null)
            {
                var defaultPrompt = new Prompt
                {
                    Name = "email_response",
                    Content = DefaultPromptContent,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Prompts.Add(defaultPrompt);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Prompt padrão criado com sucesso");
            }
        }
    }
}