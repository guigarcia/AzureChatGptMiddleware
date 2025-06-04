using Azure;
using Azure.AI.OpenAI;
using AzureChatGptMiddleware.Exceptions;
using AzureChatGptMiddleware.Models;
using Microsoft.Extensions.Options;
using OpenAI.Chat;
using System.ClientModel;
using System.Net;

namespace AzureChatGptMiddleware.Services
{
    public class AzureOpenAIService : IAzureOpenAIService
    {
        private readonly AzureOpenAIClient _azureClient;
        private readonly ChatClient _chatClient;
        private readonly AzureOpenAIOptions _options;
        private readonly ILogger<AzureOpenAIService> _logger;
        private readonly IPromptService _promptService;

        public AzureOpenAIService(
            IOptions<AzureOpenAIOptions> options,
            ILogger<AzureOpenAIService> logger,
            IPromptService promptService)
        {
            _options = options.Value;
            _logger = logger;
            _promptService = promptService;

            // Validação das opções
            if (string.IsNullOrEmpty(_options.Endpoint) || !Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out var endpoint))
            {
                throw new InvalidOperationException("Azure OpenAI Endpoint não configurado ou inválido.");
            }
            if (string.IsNullOrEmpty(_options.ApiKey))
            {
                throw new InvalidOperationException("Azure OpenAI ApiKey não configurado.");
            }
            if (string.IsNullOrEmpty(_options.DeploymentName))
            {
                throw new InvalidOperationException("Azure OpenAI DeploymentName não configurado.");
            }

            // Inicializar o cliente Azure OpenAI
            _azureClient = new AzureOpenAIClient(
                endpoint,
                new AzureKeyCredential(_options.ApiKey));

            // Obter o ChatClient para o deployment específico
            _chatClient = _azureClient.GetChatClient(_options.DeploymentName);
        }

        public async Task<string> ProcessEmailAsync(string emailContent)
        {
            try
            {
                // Buscar o prompt do sistema ativo
                var systemPrompt = await _promptService.GetActivePromptContentAsync("email_response");

                // Criar a lista de mensagens
                var messages = new List<ChatMessage>
                {
                    new SystemChatMessage(systemPrompt),
                    new UserChatMessage(emailContent)
                };

                // Configurar as opções de completação
                var chatCompletionOptions = new ChatCompletionOptions
                {
                    Temperature = 0.7f,
                    MaxOutputTokenCount = 1000
                };

                try
                {
                    // Fazer a chamada para o Azure OpenAI
                    var response = await _chatClient.CompleteChatAsync(messages, chatCompletionOptions);

                    // Verificar se há conteúdo na resposta
                    if (response.Value?.Content != null && response.Value.Content.Count > 0)
                    {
                        var firstContent = response.Value.Content[0];

                        // Retornar o texto da resposta
                        return firstContent.Text ?? "Conteúdo da mensagem da IA está nulo.";
                    }
                    else
                    {
                        _logger.LogWarning("Resposta da API Azure OpenAI bem-sucedida, mas sem conteúdo.");
                        throw new AzureOpenAICommunicationException("Resposta da API Azure OpenAI não continha conteúdo esperado.");
                    }
                }
                catch (ClientResultException clientEx)
                {
                    // Tratar erros específicos do cliente (status HTTP não-2xx)
                    _logger.LogError(clientEx, "Erro ao chamar Azure OpenAI API. Status: {Status}", clientEx.Status);

                    var errorMessage = $"Erro ao comunicar com Azure OpenAI. Status: {clientEx.Status}.";
                    if (clientEx.GetRawResponse() != null)
                    {
                        errorMessage += $" Detalhes: {clientEx.Message}";
                    }

                    // Converter o status int para HttpStatusCode
                    var httpStatusCode = (HttpStatusCode)clientEx.Status;

                    throw new AzureOpenAICommunicationException(
                        errorMessage,
                        httpStatusCode,
                        clientEx.Message,
                        clientEx);
                }
                catch (RequestFailedException requestEx)
                {
                    // Tratar erros de requisição do Azure
                    _logger.LogError(requestEx, "Erro de requisição ao chamar Azure OpenAI API. Status: {Status}", requestEx.Status);

                    // Converter o status int para HttpStatusCode
                    var httpStatusCode = (HttpStatusCode)requestEx.Status;

                    throw new AzureOpenAICommunicationException(
                        $"Erro na requisição para Azure OpenAI. Status: {requestEx.Status}.",
                        httpStatusCode,
                        requestEx.Message,
                        requestEx);
                }
                catch (TaskCanceledException timeoutEx)
                {
                    // Tratar timeouts
                    _logger.LogError(timeoutEx, "Timeout ao chamar Azure OpenAI API.");
                    throw new AzureOpenAICommunicationException("Timeout na comunicação com Azure OpenAI.", timeoutEx);
                }
                catch (Exception ex) when (ex is not AzureOpenAICommunicationException)
                {
                    // Tratar outras exceções de comunicação
                    _logger.LogError(ex, "Erro inesperado ao chamar Azure OpenAI API.");
                    throw new AzureOpenAICommunicationException("Erro inesperado na comunicação com Azure OpenAI.", ex);
                }
            }
            catch (AzureOpenAICommunicationException)
            {
                // Re-lançar exceções já tratadas
                throw;
            }
            catch (Exception ex)
            {
                // Capturar qualquer outra exceção inesperada
                _logger.LogError(ex, "Erro inesperado ao processar e-mail com Azure OpenAI.");
                throw new AzureOpenAICommunicationException("Erro inesperado no serviço Azure OpenAI.", ex);
            }
        }
    }
}