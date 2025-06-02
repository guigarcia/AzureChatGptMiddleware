// <copyright file="PromptControllerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using AzureChatGptMiddleware.Controllers;
using AzureChatGptMiddleware.Data.Entities; // Necessário para Prompt
using AzureChatGptMiddleware.Models;
using AzureChatGptMiddleware.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace AzureChatGptMiddleware.Tests
{
    public class PromptControllerTests
    {
        private readonly Mock<IPromptService> _mockPromptService;
        private readonly Mock<ILogger<PromptController>> _mockLogger;

        public PromptControllerTests()
        {
            _mockPromptService = new Mock<IPromptService>();
            _mockLogger = new Mock<ILogger<PromptController>>();
        }

        private PromptController CreateController()
        {
            return new PromptController(_mockPromptService.Object, _mockLogger.Object);
            // Se HttpContext fosse necessário:
            // var controller = new PromptController(_mockPromptService.Object, _mockLogger.Object);
            // controller.ControllerContext = new ControllerContext
            // {
            //    HttpContext = new DefaultHttpContext() // Configure conforme necessário
            // };
            // return controller;
        }

        // --- GetAll ---
        [Fact]
        public async Task GetAll_WhenServiceReturnsPrompts_ReturnsOkObjectResultWithPrompts()
        {
            // Arrange
            var prompts = new List<PromptResponse>
            {
                new PromptResponse { Id = 1, Name = "P1", Content = "C1", IsActive = true, CreatedAt = DateTime.UtcNow },
                new PromptResponse { Id = 2, Name = "P2", Content = "C2", IsActive = false, CreatedAt = DateTime.UtcNow }
            };
            _mockPromptService.Setup(s => s.GetAllPromptsAsync()).ReturnsAsync(prompts);
            var controller = CreateController();

            // Act
            var result = await controller.GetAll();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedPrompts = Assert.IsAssignableFrom<List<PromptResponse>>(okResult.Value);
            Assert.Equal(prompts.Count, returnedPrompts.Count);
        }

        [Fact]
        public async Task GetAll_WhenServiceThrowsException_ReturnsStatusCode500()
        {
            // Arrange
            _mockPromptService.Setup(s => s.GetAllPromptsAsync()).ThrowsAsync(new Exception("Service error"));
            var controller = CreateController();

            // Act
            var result = await controller.GetAll();

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
            Assert.Contains("Erro ao processar requisição", objectResult.Value.GetType().GetProperty("message").GetValue(objectResult.Value, null).ToString());
        }

        // --- GetById ---
        [Fact]
        public async Task GetById_WhenPromptExists_ReturnsOkObjectResultWithPrompt()
        {
            // Arrange
            var promptId = 1;
            var prompt = new PromptResponse { Id = promptId, Name = "P1", Content = "C1" };
            _mockPromptService.Setup(s => s.GetPromptByIdAsync(promptId)).ReturnsAsync(prompt);
            var controller = CreateController();

            // Act
            var result = await controller.GetById(promptId);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedPrompt = Assert.IsType<PromptResponse>(okResult.Value);
            Assert.Equal(promptId, returnedPrompt.Id);
        }

        [Fact]
        public async Task GetById_WhenPromptDoesNotExist_ReturnsNotFoundObjectResult()
        {
            // Arrange
            var promptId = 1;
            _mockPromptService.Setup(s => s.GetPromptByIdAsync(promptId)).ReturnsAsync((PromptResponse?)null);
            var controller = CreateController();

            // Act
            var result = await controller.GetById(promptId);

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Contains("Prompt não encontrado", notFoundResult.Value.GetType().GetProperty("message").GetValue(notFoundResult.Value, null).ToString());
        }

        [Fact]
        public async Task GetById_WhenServiceThrowsException_ReturnsStatusCode500()
        {
            // Arrange
            var promptId = 1;
            _mockPromptService.Setup(s => s.GetPromptByIdAsync(promptId)).ThrowsAsync(new Exception("Service error"));
            var controller = CreateController();

            // Act
            var result = await controller.GetById(promptId);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
        }

        // --- Create ---
        [Fact]
        public async Task Create_WithValidRequest_ReturnsCreatedAtActionResult()
        {
            // Arrange
            var request = new PromptRequest { Name = "New", Content = "Content" };
            // O Prompt retornado pelo serviço é a entidade do EF, não o PromptResponse.
            // O Controller faz o mapeamento para PromptResponse no CreatedAtAction.
            var createdEntity = new Prompt { Id = 1, Name = request.Name, Content = request.Content, IsActive = false, CreatedAt = DateTime.UtcNow };
            _mockPromptService.Setup(s => s.CreatePromptAsync(request)).ReturnsAsync(createdEntity);
            var controller = CreateController();

            // Act
            var result = await controller.Create(request);

            // Assert
            var createdAtActionResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(nameof(PromptController.GetById), createdAtActionResult.ActionName);
            Assert.Equal(createdEntity.Id, createdAtActionResult.RouteValues["id"]);
            var returnedValue = Assert.IsType<PromptResponse>(createdAtActionResult.Value);
            Assert.Equal(createdEntity.Name, returnedValue.Name);
        }

        // Testes para Create_WithInvalidRequestData (nome/conteúdo nulo/vazio) removidos.
        // Essa validação agora é esperada ser tratada pelo pipeline do FluentValidation
        // antes de atingir o método do controller em um cenário de integração.
        // Os testes unitários do controller focam na lógica do controller assumindo uma requisição válida
        // ou erros vindos do serviço.

        [Fact]
        public async Task Create_WhenServiceThrowsInvalidOperationException_ReturnsBadRequestObjectResult()
        {
            // Arrange
            var request = new PromptRequest { Name = "Duplicate", Content = "Content" };
            var exceptionMessage = "Name already exists";
            _mockPromptService.Setup(s => s.CreatePromptAsync(request)).ThrowsAsync(new InvalidOperationException(exceptionMessage));
            var controller = CreateController();

            // Act
            var result = await controller.Create(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(exceptionMessage, badRequestResult.Value.GetType().GetProperty("message").GetValue(badRequestResult.Value, null).ToString());
        }

        [Fact]
        public async Task Create_WhenServiceThrowsGeneralException_ReturnsStatusCode500()
        {
            // Arrange
            var request = new PromptRequest { Name = "New", Content = "Content" };
            _mockPromptService.Setup(s => s.CreatePromptAsync(request)).ThrowsAsync(new Exception("Service error"));
            var controller = CreateController();

            // Act
            var result = await controller.Create(request);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
        }

        // --- Update ---
        [Fact]
        public async Task Update_WithValidRequestAndId_ReturnsOkObjectResult()
        {
            // Arrange
            var promptId = 1;
            var request = new PromptRequest { Name = "Updated", Content = "Content" };
            var updatedEntity = new Prompt { Id = promptId, Name = request.Name, Content = request.Content, UpdatedAt = DateTime.UtcNow };
            _mockPromptService.Setup(s => s.UpdatePromptAsync(promptId, request)).ReturnsAsync(updatedEntity);
            var controller = CreateController();

            // Act
            var result = await controller.Update(promptId, request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnedPrompt = Assert.IsType<PromptResponse>(okResult.Value);
            Assert.Equal(updatedEntity.Name, returnedPrompt.Name);
            Assert.NotNull(returnedPrompt.UpdatedAt);
        }

        // Testes para Update_WithInvalidRequestData (nome/conteúdo nulo/vazio) removidos.
        // Validação esperada ser tratada pelo pipeline do FluentValidation.

        [Fact]
        public async Task Update_WhenServiceThrowsInvalidOperationExceptionForNotFound_ReturnsNotFoundObjectResult()
        {
            // Arrange
            var promptId = 1;
            var request = new PromptRequest { Name = "Updated", Content = "Content" };
            var exceptionMessage = "Prompt not found"; // Simula a mensagem do serviço
            _mockPromptService.Setup(s => s.UpdatePromptAsync(promptId, request)).ThrowsAsync(new InvalidOperationException(exceptionMessage));
            var controller = CreateController();

            // Act
            var result = await controller.Update(promptId, request);

            // Assert
            // O controller PromptController.cs especificamente trata InvalidOperationException do UpdatePromptAsync como NotFound.
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(exceptionMessage, notFoundResult.Value.GetType().GetProperty("message").GetValue(notFoundResult.Value, null).ToString());
        }


        [Fact]
        public async Task Update_WhenServiceThrowsGeneralException_ReturnsStatusCode500()
        {
            // Arrange
            var promptId = 1;
            var request = new PromptRequest { Name = "Updated", Content = "Content" };
            _mockPromptService.Setup(s => s.UpdatePromptAsync(promptId, request)).ThrowsAsync(new SystemException("General service error")); // Use SystemException to differentiate from InvalidOperationException
            var controller = CreateController();

            // Act
            var result = await controller.Update(promptId, request);

            // Assert
            var objectResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, objectResult.StatusCode);
        }
    }
}
