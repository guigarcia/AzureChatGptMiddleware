using Microsoft.EntityFrameworkCore;
using AzureChatGptMiddleware.Data;
using AzureChatGptMiddleware.Data.Entities;
using AzureChatGptMiddleware.Models;

namespace AzureChatGptMiddleware.Services
{
    /// <summary>
    /// Serviço para gerenciar os prompts do sistema armazenados no banco de dados.
    /// Inclui operações CRUD para prompts e a lógica para obter o prompt ativo para uma determinada funcionalidade.
    /// </summary>
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
            // Tenta buscar o prompt ativo mais recentemente atualizado (ou criado) com o nome fornecido.
            // Um prompt é considerado "ativo" se sua flag IsActive for true.
            // Se múltiplos prompts com o mesmo nome estiverem ativos (o que não deveria ser uma configuração normal),
            // o mais recente (baseado em UpdatedAt, depois CreatedAt) será usado.
            var prompt = await _context.Prompts
                .Where(p => p.Name == name && p.IsActive)
                .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt) 
                .FirstOrDefaultAsync();

            if (prompt == null)
            {
                // Se nenhum prompt ativo com o nome especificado for encontrado,
                // registra um aviso e retorna o DefaultPromptContent definido nesta classe.
                // Este DefaultPromptContent também é usado por EnsureDefaultPromptAsync se "email_response" não existir.
                _logger.LogWarning($"Prompt ativo com nome '{name}' não encontrado no banco de dados. Usando prompt padrão interno do serviço.");
                return DefaultPromptContent;
            }

            return prompt.Content;
        }

        public async Task<Prompt> CreatePromptAsync(PromptRequest request)
        {
            // Verifica se já existe um prompt com o mesmo nome para evitar duplicatas.
            // Idealmente, o campo Name teria uma restrição UNIQUE no banco de dados também.
            var existingPrompt = await _context.Prompts
                .FirstOrDefaultAsync(p => p.Name == request.Name);

            if (existingPrompt != null)
            {
                // Lança exceção se um prompt com o mesmo nome já existir.
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

            // Verifica se o novo nome já está sendo utilizado por outro prompt
            if (!string.Equals(prompt.Name, request.Name, StringComparison.OrdinalIgnoreCase))
            {
                var duplicate = await _context.Prompts
                    .AnyAsync(p => p.Name == request.Name && p.Id != id);

                if (duplicate)
                {
                    throw new InvalidOperationException($"Já existe um prompt com o nome '{request.Name}'");
                }
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
            // Verifica se um prompt com o nome "email_response" já existe no banco.
            // Este é considerado o prompt padrão para a funcionalidade de resposta de e-mail.
            var existingPrompt = await _context.Prompts
                .FirstOrDefaultAsync(p => p.Name == "email_response");

            if (existingPrompt == null)
            {
                // Se não existir, cria um novo prompt "email_response"
                // utilizando o DefaultPromptContent definido nesta classe.
                // Este prompt é marcado como ativo por padrão.
                var defaultPrompt = new Prompt
                {
                    Name = "email_response", // Nome padrão para o prompt de e-mail
                    Content = DefaultPromptContent, // Conteúdo padrão definido na constante
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Prompts.Add(defaultPrompt);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Prompt padrão 'email_response' criado com sucesso pois não existia no banco.");
            }
            // Se o prompt "email_response" já existir, este método não faz nada,
            // preservando qualquer personalização que possa ter sido feita nele.
        }
    }
}