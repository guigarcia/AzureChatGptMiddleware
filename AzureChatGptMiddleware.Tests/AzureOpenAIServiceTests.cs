// <copyright file="AzureOpenAIServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using AzureChatGptMiddleware.Services;
using AzureChatGptMiddleware.Models; // Added
using AzureChatGptMiddleware.Exceptions; // Added
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options; // Added
using Moq;
using Moq.Protected;
using System;
using System.Linq; // Added
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AzureChatGptMiddleware.Tests
{
    public class AzureOpenAIServiceTests
    {
        private readonly Mock<IOptions<AzureOpenAIOptions>> _mockOptions;
        private readonly Mock<ILogger<AzureOpenAIService>> _mockLogger;
        private readonly Mock<IPromptService> _mockPromptService;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private HttpRequestMessage? _capturedRequest;

        public AzureOpenAIServiceTests()
        {
            _mockOptions = new Mock<IOptions<AzureOpenAIOptions>>();
            _mockLogger = new Mock<ILogger<AzureOpenAIService>>();
            _mockPromptService = new Mock<IPromptService>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();

            SetupDefaultOptions();
        }

        private void SetupDefaultOptions(Action<AzureOpenAIOptions>? configure = null)
        {
            var options = new AzureOpenAIOptions
            {
                Endpoint = "https://test.openai.azure.com/",
                ApiKey = "test-api-key",
                DeploymentName = "test-deployment",
                ApiVersion = "2024-02-01"
            };
            configure?.Invoke(options); // Allow overriding specific options for a test
            _mockOptions.Setup(o => o.Value).Returns(options);
        }

        private AzureOpenAIService CreateService(HttpClient? httpClient = null)
        {
            httpClient ??= new HttpClient(_mockHttpMessageHandler.Object);
            return new AzureOpenAIService(
                httpClient,
                _mockOptions.Object,
                _mockLogger.Object,
                _mockPromptService.Object
            );
        }

        // --- Testes do Construtor (Validação de Opções) ---
        // Estes testes são mais relevantes se a validação de opções em Program.cs falhar
        // ou se quisermos garantir que o serviço em si também valida.
        // A refatoração do AzureOpenAIService manteve as validações no construtor.

        [Fact]
        public void Constructor_WhenOptionsEndpointIsNull_ShouldThrowInvalidOperationException()
        {
            // Arrange
            SetupDefaultOptions(opt => opt.Endpoint = null!); // Force null endpoint

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => CreateService());
            Assert.Equal("Azure OpenAI Endpoint não configurado ou inválido.", ex.Message);
        }

        [Fact]
        public void Constructor_WhenOptionsEndpointIsInvalidUri_ShouldThrowInvalidOperationException()
        {
            // Arrange
            SetupDefaultOptions(opt => opt.Endpoint = " ليست يو آر إل"); // Invalid URI

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => CreateService());
            Assert.Equal("Azure OpenAI Endpoint não configurado ou inválido.", ex.Message);
        }


        [Fact]
        public void Constructor_WhenOptionsApiKeyIsNull_ShouldThrowInvalidOperationException()
        {
            // Arrange
            SetupDefaultOptions(opt => opt.ApiKey = null!); // Force null ApiKey

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => CreateService());
            Assert.Equal("Azure OpenAI ApiKey não configurado.", ex.Message);
        }


        // --- Testes de ProcessEmailAsync ---

        [Fact]
        public async Task ProcessEmailAsync_SuccessfulResponse_ReturnsContentAndUsesCorrectRequestDetails()
        {
            // Arrange
            var emailContent = "Test email content";
            var systemPrompt = "Test system prompt";
            var expectedApiResponse = "AI generated response";
            
            var currentOptions = _mockOptions.Object.Value; // Get options used by the service

            _mockPromptService.Setup(s => s.GetActivePromptContentAsync("email_response"))
                .ReturnsAsync(systemPrompt);

            var responseJson = JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content = expectedApiResponse } } }
            });
            var httpResponse = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => _capturedRequest = req)
                .ReturnsAsync(httpResponse);

            var service = CreateService();

            // Act
            var result = await service.ProcessEmailAsync(emailContent);

            // Assert
            Assert.Equal(expectedApiResponse, result);
            _mockPromptService.Verify(s => s.GetActivePromptContentAsync("email_response"), Times.Once);

            Assert.NotNull(_capturedRequest);
            Assert.Equal(HttpMethod.Post, _capturedRequest.Method);
            Assert.Equal(currentOptions.ApiKey, _capturedRequest.Headers.GetValues("api-key").FirstOrDefault());
            Assert.Contains(currentOptions.DeploymentName, _capturedRequest.RequestUri.ToString());
            Assert.Contains(currentOptions.ApiVersion, _capturedRequest.RequestUri.ToString());
            Assert.StartsWith(currentOptions.Endpoint, _capturedRequest.RequestUri.AbsoluteUri);

            var requestBody = await _capturedRequest.Content.ReadAsStringAsync();
            var requestJson = JsonDocument.Parse(requestBody);
            Assert.Equal(systemPrompt, requestJson.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
            Assert.Equal(emailContent, requestJson.RootElement.GetProperty("messages")[1].GetProperty("content").GetString());
        }

        [Theory]
        [InlineData(HttpStatusCode.InternalServerError)]
        [InlineData(HttpStatusCode.BadRequest)]
        [InlineData(HttpStatusCode.Unauthorized)]
        public async Task ProcessEmailAsync_ApiFailure_ThrowsAzureOpenAIComunicationExceptionAndLogsError(HttpStatusCode failureCode)
        {
            // Arrange
            var emailContent = "Test email content";
            var systemPrompt = "Test system prompt";
            var errorResponseBody = $"{{ \"error\": {{ \"message\": \"API Error {failureCode}\" }} }}";


            _mockPromptService.Setup(s => s.GetActivePromptContentAsync("email_response"))
                .ReturnsAsync(systemPrompt);

            var httpResponse = new HttpResponseMessage
            {
                StatusCode = failureCode,
                Content = new StringContent(errorResponseBody)
            };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(httpResponse);

            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AzureOpenAIComunicationException>(() => service.ProcessEmailAsync(emailContent));
            Assert.Equal($"Erro ao comunicar com Azure OpenAI. Status: {failureCode}.", ex.Message);
            Assert.Equal(failureCode, ex.StatusCode);
            Assert.Equal(errorResponseBody, ex.ErrorResponseContent);

            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Erro na API Azure OpenAI. Status: {failureCode}. Resposta: {errorResponseBody}")),
                    null, // Exception is now part of the custom exception, not passed directly to this top-level log for this case
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
        
        [Fact]
        public async Task ProcessEmailAsync_HttpRequestException_ThrowsAzureOpenAIComunicationException()
        {
            // Arrange
            _mockPromptService.Setup(s => s.GetActivePromptContentAsync("email_response")).ReturnsAsync("prompt");
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("Network error"));
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AzureOpenAIComunicationException>(() => service.ProcessEmailAsync("test"));
            Assert.Equal("Erro na comunicação HTTP com Azure OpenAI.", ex.Message);
            Assert.IsType<HttpRequestException>(ex.InnerException);
        }

        [Fact]
        public async Task ProcessEmailAsync_TimeoutException_ThrowsAzureOpenAIComunicationException()
        {
            // Arrange
            _mockPromptService.Setup(s => s.GetActivePromptContentAsync("email_response")).ReturnsAsync("prompt");
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new TaskCanceledException("Timeout")); // Simulate timeout
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AzureOpenAIComunicationException>(() => service.ProcessEmailAsync("test"));
            Assert.Equal("Timeout na comunicação com Azure OpenAI.", ex.Message);
            Assert.IsType<TaskCanceledException>(ex.InnerException);
        }


        [Fact]
        public async Task ProcessEmailAsync_WhenPromptServiceReturnsNullOrEmpty_UsesItAndProceeds()
        {
            // Arrange
            var emailContent = "Test email content";
            string? systemPrompt = null;
            var expectedApiResponse = "AI response with null system prompt";

            _mockPromptService.Setup(s => s.GetActivePromptContentAsync("email_response"))
                .ReturnsAsync(systemPrompt);

            var responseJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = expectedApiResponse } } } });
            var httpResponse = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(responseJson) };

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((req, _) => _capturedRequest = req)
                .ReturnsAsync(httpResponse);
            var service = CreateService();

            // Act
            var result = await service.ProcessEmailAsync(emailContent);

            // Assert
            Assert.Equal(expectedApiResponse, result);
            var requestBody = await _capturedRequest.Content.ReadAsStringAsync();
            var requestJson = JsonDocument.Parse(requestBody);
            Assert.Equal(systemPrompt, requestJson.RootElement.GetProperty("messages")[0].GetProperty("content").GetString());
        }


        [Fact]
        public async Task ProcessEmailAsync_MalformedApiResponse_NoChoices_ThrowsAzureOpenAIComunicationException()
        {
            // Arrange
            _mockPromptService.Setup(s => s.GetActivePromptContentAsync("email_response")).ReturnsAsync("prompt");
            var responseJson = JsonSerializer.Serialize(new { something_else = "data" });
            var httpResponse = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(responseJson) };
            _mockHttpMessageHandler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()).ReturnsAsync(httpResponse);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AzureOpenAIComunicationException>(() => service.ProcessEmailAsync("test"));
            Assert.Equal("Resposta da API Azure OpenAI com formato inesperado.", ex.Message); // Due to KeyNotFoundException
            Assert.IsType<KeyNotFoundException>(ex.InnerException);
        }

        [Fact]
        public async Task ProcessEmailAsync_MalformedApiResponse_NoMessageInChoice_ThrowsAzureOpenAIComunicationException()
        {
            // Arrange
            _mockPromptService.Setup(s => s.GetActivePromptContentAsync("email_response")).ReturnsAsync("prompt");
            var responseJson = JsonSerializer.Serialize(new { choices = new[] { new { no_message_here = "data" } } });
            var httpResponse = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(responseJson) };
            _mockHttpMessageHandler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()).ReturnsAsync(httpResponse);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AzureOpenAIComunicationException>(() => service.ProcessEmailAsync("test"));
            Assert.Equal("Resposta da API Azure OpenAI com formato inesperado.", ex.Message); // Due to KeyNotFoundException
            Assert.IsType<KeyNotFoundException>(ex.InnerException);
        }

        [Fact]
        public async Task ProcessEmailAsync_MalformedApiResponse_NoContentInMessage_ThrowsAzureOpenAIComunicationException()
        {
            // Arrange
            _mockPromptService.Setup(s => s.GetActivePromptContentAsync("email_response")).ReturnsAsync("prompt");
            var responseJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { no_content_here = "data" } } } });
            var httpResponse = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(responseJson) };
            _mockHttpMessageHandler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()).ReturnsAsync(httpResponse);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AzureOpenAIComunicationException>(() => service.ProcessEmailAsync("test"));
            Assert.Equal("Resposta da API Azure OpenAI com formato inesperado.", ex.Message); // Due to KeyNotFoundException
            Assert.IsType<KeyNotFoundException>(ex.InnerException);
        }
        
        [Fact]
        public async Task ProcessEmailAsync_MalformedApiResponse_ContentIsNull_ReturnsSpecificMessage()
        {
            // Arrange
            _mockPromptService.Setup(s => s.GetActivePromptContentAsync("email_response")).ReturnsAsync("prompt");
            var responseJson = JsonSerializer.Serialize(new { choices = new[] { new { message = new { content = (string?)null } } } });
            var httpResponse = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(responseJson) };
            _mockHttpMessageHandler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()).ReturnsAsync(httpResponse);
            var service = CreateService();

            // Act
            var result = await service.ProcessEmailAsync("test");
            
            // Assert
            Assert.Equal("Conteúdo da mensagem da IA está nulo.", result);
        }


         [Fact]
        public async Task ProcessEmailAsync_ApiResponse_EmptyChoicesArray_ThrowsAzureOpenAIComunicationException()
        {
            // Arrange
            _mockPromptService.Setup(s => s.GetActivePromptContentAsync("email_response")).ReturnsAsync("prompt");
            var responseJson = JsonSerializer.Serialize(new { choices = Array.Empty<object>() });
            var httpResponse = new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(responseJson) };
            _mockHttpMessageHandler.Protected().Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>()).ReturnsAsync(httpResponse);
            var service = CreateService();

            // Act & Assert
            var ex = await Assert.ThrowsAsync<AzureOpenAIComunicationException>(() => service.ProcessEmailAsync("test"));
            Assert.Equal("Resposta da API Azure OpenAI não continha 'choices' esperados.", ex.Message);
            // No InnerException in this specific path of the refactored code
        }
    }
}
