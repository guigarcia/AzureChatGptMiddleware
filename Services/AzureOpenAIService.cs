using AzureChatGptMiddleware.Exceptions;
using AzureChatGptMiddleware.Models;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text;
using System.Text.Json;


namespace AzureChatGptMiddleware.Services
{
    public class AzureOpenAIService : IAzureOpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly AzureOpenAIOptions _options;
        private readonly ILogger<AzureOpenAIService> _logger;
        private readonly IPromptService _promptService;

        public AzureOpenAIService(
            HttpClient httpClient,
            IOptions<AzureOpenAIOptions> options, // Changed from IConfiguration
            ILogger<AzureOpenAIService> logger,
            IPromptService promptService)
        {
            _httpClient = httpClient;
            _options = options.Value; // Get the actual options object
            _logger = logger;
            _promptService = promptService;

            // Validation of options should be handled by IOptions<TOptions> validation at startup
            // or by checking options.Value directly here if preferred for runtime checks.
            // For this refactor, we assume startup validation.
            if (string.IsNullOrEmpty(_options.Endpoint) || !Uri.TryCreate(_options.Endpoint, UriKind.Absolute, out _))
            {
                // This should ideally be caught by startup validation.
                throw new InvalidOperationException("Azure OpenAI Endpoint não configurado ou inválido.");
            }
            if (string.IsNullOrEmpty(_options.ApiKey))
            {
                // This should ideally be caught by startup validation.
                throw new InvalidOperationException("Azure OpenAI ApiKey não configurado.");
            }

            _httpClient.BaseAddress = new Uri(_options.Endpoint);
            _httpClient.DefaultRequestHeaders.Add("api-key", _options.ApiKey);
        }

        public async Task<string> ProcessEmailAsync(string emailContent)
        {
            // O método ProcessEmailAsync orquestra a chamada à API do Azure OpenAI:
            // 1. Busca o prompt de sistema ativo usando IPromptService.
            // 2. Monta o corpo da requisição com o prompt do sistema e o conteúdo do e-mail do usuário.
            // 3. Serializa o corpo da requisição para JSON.
            // 4. Envia a requisição POST para o endpoint configurado do Azure OpenAI.
            // 5. Trata a resposta:
            //    - Em caso de sucesso (HTTP 2xx):
            //        - Lê e parseia o conteúdo JSON da resposta.
            //        - Extrai o texto da primeira "choice" da IA.
            //        - Retorna o texto da IA ou uma mensagem de erro se o conteúdo estiver nulo.
            //    - Em caso de falha na chamada HTTP (ex: timeout, erro de rede):
            //        - Lança AzureOpenAIComunicationException com detalhes do erro HTTP.
            //    - Em caso de resposta da API com status de erro (não-2xx):
            //        - Lê o corpo do erro da API.
            //        - Lança AzureOpenAIComunicationException com status code e corpo do erro.
            //    - Em caso de JSON de resposta bem-sucedida, mas malformado ou com campos ausentes:
            //        - Lança AzureOpenAIComunicationException com detalhes do erro de parsing.
            // 6. Exceções inesperadas durante o processo também são capturadas e encapsuladas em AzureOpenAIComunicationException.
            try
            {
                var systemPrompt = await _promptService.GetActivePromptContentAsync("email_response");

                // As configurações de DeploymentName e ApiVersion são obtidas de _options injetado.
                // Temperature e MaxTokens são atualmente fixos, mas podem ser movidos para _options se necessário.
                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = emailContent }
                    },
                    temperature = 0.7, // Consider making these configurable via _options
                    max_tokens = 1000  // Consider making these configurable via _options
                };

                var jsonPayload = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                var requestUri = $"openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";
                
                HttpResponseMessage response;
                try
                {
                    // Envia a requisição para a API do Azure OpenAI.
                    response = await _httpClient.PostAsync(requestUri, content);
                }
                catch (HttpRequestException httpEx) // Erros de rede, DNS, etc.
                {
                    _logger.LogError(httpEx, "Erro de HttpRequest ao chamar Azure OpenAI API. URI: {RequestUri}", requestUri);
                    throw new AzureOpenAIComunicationException($"Erro na comunicação HTTP com Azure OpenAI ao tentar acessar {requestUri}.", httpEx);
                }
                catch (TaskCanceledException timeoutEx) // Captura timeouts do HttpClient.
                {
                    _logger.LogError(timeoutEx, "Timeout ao chamar Azure OpenAI API. URI: {RequestUri}", requestUri);
                    throw new AzureOpenAIComunicationException($"Timeout na comunicação com Azure OpenAI ao tentar acessar {requestUri}.", timeoutEx);
                }

                // Processa a resposta da API.
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    try
                    {
                        // Tenta parsear o JSON e extrair o conteúdo da mensagem.
                        using var document = JsonDocument.Parse(responseContent);
                        var choices = document.RootElement.GetProperty("choices");
                        if (choices.GetArrayLength() > 0)
                        {
                            var firstChoice = choices[0];
                            var message = firstChoice.GetProperty("message");
                            var messageContentElement = message.GetProperty("content"); // Nome corrigido para corresponder ao uso
                            return messageContentElement.GetString() ?? "Conteúdo da mensagem da IA está nulo.";
                        }
                        else
                        {
                            // Resposta OK, mas sem o array 'choices' esperado.
                            _logger.LogWarning("Resposta da API Azure OpenAI bem-sucedida (Status: {StatusCode}), mas array 'choices' está vazio. Conteúdo: {ResponseContent}", response.StatusCode, responseContent);
                            throw new AzureOpenAIComunicationException($"Resposta da API Azure OpenAI (Status: {response.StatusCode}) não continha 'choices' esperados.");
                        }
                    }
                    catch (JsonException jsonEx) // Erro ao parsear o JSON.
                    {
                        _logger.LogError(jsonEx, "Erro ao parsear JSON da resposta da API Azure OpenAI (Status: {StatusCode}). Conteúdo: {ResponseContent}", response.StatusCode, responseContent);
                        throw new AzureOpenAIComunicationException($"Erro ao interpretar a resposta JSON da API Azure OpenAI (Status: {response.StatusCode}).", jsonEx);
                    }
                    catch (KeyNotFoundException keyEx) // Estrutura do JSON diferente do esperado.
                    {
                        _logger.LogError(keyEx, "Campo esperado não encontrado no JSON da resposta da API Azure OpenAI (Status: {StatusCode}). Conteúdo: {ResponseContent}", response.StatusCode, responseContent);
                        throw new AzureOpenAIComunicationException($"Resposta da API Azure OpenAI (Status: {response.StatusCode}) com formato inesperado.", keyEx);
                    }
                }
                else
                {
                    // A API retornou um status code de erro (não-2xx).
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogError("Erro na API Azure OpenAI. Status: {StatusCode}. Resposta: {ErrorContent}. URI: {RequestUri}", response.StatusCode, errorContent, requestUri);
                    throw new AzureOpenAIComunicationException(
                        $"Erro ao comunicar com Azure OpenAI. Status: {response.StatusCode}. URI: {requestUri}.",
                        response.StatusCode,
                        errorContent);
                }
            }
            catch (AzureOpenAIComunicationException) // Se já for a exceção customizada, apenas a relança.
            {
                throw;
            }
            catch (Exception ex) // Captura qualquer outra exceção inesperada.
            {
                _logger.LogError(ex, "Erro inesperado ao processar e-mail com Azure OpenAI.");
                // Encapsula em AzureOpenAIComunicationException para padronizar o tratamento de erros.
                throw new AzureOpenAIComunicationException("Erro inesperado no serviço Azure OpenAI.", ex);
            }
        }
    }
}
