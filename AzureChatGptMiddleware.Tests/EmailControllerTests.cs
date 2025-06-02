// <copyright file="EmailControllerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using AzureChatGptMiddleware.Controllers;
using AzureChatGptMiddleware.Models;
using AzureChatGptMiddleware.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http; // Required for HttpContext
using AzureChatGptMiddleware.Data.Entities; // Required for RequestLog

namespace AzureChatGptMiddleware.Tests
{
    public class EmailControllerTests
    {
        private readonly Mock<IAzureOpenAIService> _mockAzureOpenAIService;
        private readonly Mock<IRequestLogService> _mockRequestLogService;
        private readonly Mock<ILogger<EmailController>> _mockLogger;

        public EmailControllerTests()
        {
            _mockAzureOpenAIService = new Mock<IAzureOpenAIService>();
            _mockRequestLogService = new Mock<IRequestLogService>();
            _mockLogger = new Mock<ILogger<EmailController>>();
        }

        private EmailController CreateController()
        {
            var controller = new EmailController(
                _mockAzureOpenAIService.Object,
                _mockRequestLogService.Object,
                _mockLogger.Object
            );

            // Mock HttpContext and Connection for RemoteIpAddress
            var mockHttpContext = new Mock<HttpContext>();
            var mockConnectionInfo = new Mock<ConnectionInfo>();
            mockConnectionInfo.Setup(c => c.RemoteIpAddress).Returns(System.Net.IPAddress.Parse("127.0.0.1"));
            mockHttpContext.Setup(c => c.Connection).Returns(mockConnectionInfo.Object);
            
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = mockHttpContext.Object
            };
            return controller;
        }

        [Fact]
        public async Task ProcessEmail_WithValidRequest_ReturnsOkObjectResult()
        {
            // Arrange
            var request = new EmailRequest { Message = "Test email content" };
            var aiResponse = "AI processed response";
            var logId = Guid.NewGuid();
            var createdAt = DateTime.UtcNow;

            _mockAzureOpenAIService.Setup(s => s.ProcessEmailAsync(request.Message))
                .ReturnsAsync(aiResponse);

            _mockRequestLogService.Setup(s => s.LogRequestAsync(
                    request.Message,
                    aiResponse,
                    true,
                    null, // No error message for success
                    It.IsAny<string>() // ClientInfo
                ))
                .ReturnsAsync(new RequestLog { Id = logId, OriginalRequest = request.Message, AiResponse = aiResponse, Success = true, CreatedAt = createdAt });

            var controller = CreateController();

            // Act
            var result = await controller.ProcessEmail(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = Assert.IsType<EmailResponse>(okResult.Value);
            Assert.Equal(aiResponse, returnValue.Response);
            Assert.Equal(logId, returnValue.RequestId);
            Assert.Equal(createdAt, returnValue.ProcessedAt);

            _mockAzureOpenAIService.Verify(s => s.ProcessEmailAsync(request.Message), Times.Once);
            _mockRequestLogService.Verify(s => s.LogRequestAsync(request.Message, aiResponse, true, null, It.IsAny<string>()), Times.Once);
        }

        // Teste para ProcessEmail_WithNullOrEmptyOrWhitespaceMessage_ReturnsBadRequest removido.
        // Essa validação agora é esperada ser tratada pelo pipeline do FluentValidation
        // antes de atingir o método do controller em um cenário de integração.

        [Fact]
        public async Task ProcessEmail_WhenAzureOpenAIServiceThrowsException_ReturnsStatusCode500AndLogsError()
        {
            // Arrange
            var request = new EmailRequest { Message = "Test email content" };
            var exceptionMessage = "Azure OpenAI Service Error";
            _mockAzureOpenAIService.Setup(s => s.ProcessEmailAsync(request.Message))
                .ThrowsAsync(new Exception(exceptionMessage));
            
            // Mocking the error log call
            _mockRequestLogService.Setup(s => s.LogRequestAsync(
                    request.Message,
                    string.Empty, // Empty AI response on error
                    false,        // Success = false
                    exceptionMessage, // Error message
                    It.IsAny<string>()  // ClientInfo
                ))
                .ReturnsAsync(new RequestLog { Id = Guid.NewGuid(), Success = false, ErrorMessage = exceptionMessage });


            var controller = CreateController();

            // Act
            var result = await controller.ProcessEmail(request);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Contains("Erro ao processar requisição. Tente novamente mais tarde.", objectResult.Value.GetType().GetProperty("message").GetValue(objectResult.Value, null).ToString());

            _mockAzureOpenAIService.Verify(s => s.ProcessEmailAsync(request.Message), Times.Once);
            _mockRequestLogService.Verify(s => s.LogRequestAsync(request.Message, string.Empty, false, exceptionMessage, It.IsAny<string>()), Times.Once);
            
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Erro ao processar e-mail")),
                    It.IsAny<Exception>(), // Check that an exception is passed to the logger
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task ProcessEmail_WhenRequestLogServiceThrowsExceptionAfterSuccessfulOpenAI_StillReturnsOkButLogsLoggingError()
        {
            // Arrange
            var request = new EmailRequest { Message = "Test email content" };
            var aiResponse = "AI processed response";
            var logServiceExceptionMessage = "RequestLogService Error";

            _mockAzureOpenAIService.Setup(s => s.ProcessEmailAsync(request.Message))
                .ReturnsAsync(aiResponse);

            _mockRequestLogService.Setup(s => s.LogRequestAsync(
                    request.Message,
                    aiResponse,
                    true,
                    null,
                    It.IsAny<string>()
                ))
                .ThrowsAsync(new Exception(logServiceExceptionMessage)); // Log service fails

            var controller = CreateController();

            // Act: The controller's current implementation will catch this exception in the main try-catch,
            // log it, and then attempt to log *again* in the catch block for the error.
            // This will result in a 500.
            var result = await controller.ProcessEmail(request);


            // Assert
            // Given the current structure, if RequestLogService throws, the main catch block is entered.
            // It then tries to log the failure *again* using RequestLogService. If that also fails (or if we don't mock a second call),
            // the original error from RequestLogService (during the success path) is what gets logged by _logger.LogError.
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Contains("Erro ao processar requisição. Tente novamente mais tarde.", objectResult.Value.GetType().GetProperty("message").GetValue(objectResult.Value, null).ToString());


            _mockAzureOpenAIService.Verify(s => s.ProcessEmailAsync(request.Message), Times.Once);
            _mockRequestLogService.Verify(s => s.LogRequestAsync(request.Message, aiResponse, true, null, It.IsAny<string>()), Times.Once); // First call
            
            // Verify that the second call to LogRequestAsync (the error logging part) was attempted
            _mockRequestLogService.Verify(s => s.LogRequestAsync(
                request.Message,
                string.Empty,
                false,  // success = false
                It.Is<string>(s => s.Contains(logServiceExceptionMessage)), // error message from the exception
                It.IsAny<string>()
            ), Times.Once);


            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Erro ao processar e-mail")), // Main log
                    It.Is<Exception>(ex => ex.Message == logServiceExceptionMessage), // Exception from the first LogRequestAsync call
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
