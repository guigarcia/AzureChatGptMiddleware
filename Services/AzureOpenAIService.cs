using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace AzureChatGptMiddleware.Services
{
    public class AzureOpenAIService : IAzureOpenAIService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureOpenAIService> _logger;
        private readonly IPromptService _promptService;

        public AzureOpenAIService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<AzureOpenAIService> logger,
            IPromptService promptService)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
            _promptService = promptService;

            var azureConfig = _configuration.GetSection("AzureOpenAI");
            _httpClient.BaseAddress = new Uri(azureConfig["Endpoint"] ?? throw new InvalidOperationException("Azure OpenAI Endpoint não configurado"));
            _httpClient.DefaultRequestHeaders.Add("api-key", azureConfig["ApiKey"]);
        }

        public async Task<string> ProcessEmailAsync(string emailContent)
        {
            try
            {
                // Buscar prompt ativo do banco de dados
                var systemPrompt = await _promptService.GetActivePromptContentAsync("email_response");

                var azureConfig = _configuration.GetSection("AzureOpenAI");
                var deploymentName = azureConfig["DeploymentName"];
                var apiVersion = azureConfig["ApiVersion"];

                var requestBody = new
                {
                    messages = new[]
                    {
                        new { role = "system", content = systemPrompt },
                        new { role = "user", content = emailContent }
                    },
                    temperature = 0.7,
                    max_tokens = 1000
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(
                    $"openai/deployments/{deploymentName}/chat/completions?api-version={apiVersion}",
                    content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    using var document = JsonDocument.Parse(responseContent);

                    var choices = document.RootElement.GetProperty("choices");
                    if (choices.GetArrayLength() > 0)
                    {
                        var firstChoice = choices[0];
                        var message = firstChoice.GetProperty("message");
                        return message.GetProperty("content").GetString() ?? "Erro ao processar resposta";
                    }
                }

                _logger.LogError($"Erro na API Azure OpenAI: {response.StatusCode}");
                throw new Exception($"Erro ao comunicar com Azure OpenAI: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar e-mail com Azure OpenAI");
                throw;
            }
        }
    }
}
