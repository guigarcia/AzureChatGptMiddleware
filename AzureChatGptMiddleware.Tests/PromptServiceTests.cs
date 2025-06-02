// <copyright file="PromptServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using AzureChatGptMiddleware.Data;
using AzureChatGptMiddleware.Data.Entities;
using AzureChatGptMiddleware.Models;
using AzureChatGptMiddleware.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AzureChatGptMiddleware.Tests
{
    public class PromptServiceTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()) // Unique name for each test
                .Options;
            var dbContext = new ApplicationDbContext(options);
            dbContext.Database.EnsureCreated(); // Ensure the schema is created
            return dbContext;
        }

        private Mock<ILogger<PromptService>> GetLoggerMock()
        {
            return new Mock<ILogger<PromptService>>();
        }

        [Fact]
        public async Task GetAllPromptsAsync_WhenNoPrompts_ShouldReturnEmptyList()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);

            // Act
            var result = await service.GetAllPromptsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetAllPromptsAsync_WhenPromptsExist_ShouldReturnListOfPrompts()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);

            dbContext.Prompts.AddRange(
                new Prompt { Name = "Test1", Content = "Content1", CreatedAt = DateTime.UtcNow, IsActive = true },
                new Prompt { Name = "Test2", Content = "Content2", CreatedAt = DateTime.UtcNow, IsActive = false }
            );
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetAllPromptsAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetPromptByIdAsync_WithExistingId_ShouldReturnPrompt()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);

            var prompt = new Prompt { Name = "Test1", Content = "Content1", CreatedAt = DateTime.UtcNow, IsActive = true };
            dbContext.Prompts.Add(prompt);
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetPromptByIdAsync(prompt.Id);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(prompt.Name, result.Name);
        }

        [Fact]
        public async Task GetPromptByIdAsync_WithNonExistingId_ShouldReturnNull()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);

            // Act
            var result = await service.GetPromptByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task CreatePromptAsync_WithValidData_ShouldCreatePrompt()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);
            var request = new PromptRequest { Name = "NewPrompt", Content = "NewContent", IsActive = true };
            var initialTime = DateTime.UtcNow.AddSeconds(-1); // ensure CreatedAt is after this

            // Act
            var result = await service.CreatePromptAsync(request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.Name, result.Name);
            Assert.Equal(request.Content, result.Content);
            Assert.Equal(request.IsActive, result.IsActive);
            Assert.True(result.CreatedAt >= initialTime); // Check if CreatedAt is set

            var promptInDb = await dbContext.Prompts.FindAsync(result.Id);
            Assert.NotNull(promptInDb);
            Assert.Equal(request.Name, promptInDb.Name);
        }

        [Fact]
        public async Task CreatePromptAsync_WithDuplicateName_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);

            var existingPromptName = "ExistingPrompt";
            dbContext.Prompts.Add(new Prompt { Name = existingPromptName, Content = "Content1", CreatedAt = DateTime.UtcNow, IsActive = true });
            await dbContext.SaveChangesAsync();

            var request = new PromptRequest { Name = existingPromptName, Content = "NewContent", IsActive = true };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreatePromptAsync(request));
            Assert.Equal($"Já existe um prompt com o nome '{existingPromptName}'", exception.Message);
        }

        [Fact]
        public async Task UpdatePromptAsync_WithExistingIdAndValidData_ShouldUpdatePrompt()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);

            var originalPrompt = new Prompt { Name = "OriginalName", Content = "OriginalContent", CreatedAt = DateTime.UtcNow, IsActive = true };
            dbContext.Prompts.Add(originalPrompt);
            await dbContext.SaveChangesAsync();
            dbContext.Entry(originalPrompt).State = EntityState.Detached; // Detach to avoid tracking issues

            var request = new PromptRequest { Name = "UpdatedName", Content = "UpdatedContent", IsActive = false };
            var initialTime = DateTime.UtcNow.AddSeconds(-1);

            // Act
            var result = await service.UpdatePromptAsync(originalPrompt.Id, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.Name, result.Name);
            Assert.Equal(request.Content, result.Content);
            Assert.Equal(request.IsActive, result.IsActive);
            Assert.NotNull(result.UpdatedAt);
            Assert.True(result.UpdatedAt >= initialTime); // Check if UpdatedAt is set

            var promptInDb = await dbContext.Prompts.FindAsync(originalPrompt.Id);
            Assert.NotNull(promptInDb);
            Assert.Equal(request.Name, promptInDb.Name);
            Assert.NotNull(promptInDb.UpdatedAt);
        }

        [Fact]
        public async Task UpdatePromptAsync_WithNonExistingId_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);
            var request = new PromptRequest { Name = "UpdatedName", Content = "UpdatedContent", IsActive = false };
            var nonExistingId = 999;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePromptAsync(nonExistingId, request));
            Assert.Equal($"Prompt com ID {nonExistingId} não encontrado", exception.Message);
        }
        
        [Fact]
        public async Task UpdatePromptAsync_WithDuplicateNameOnAnotherRecord_ShouldNotThrowException()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);

            var promptToUpdate = new Prompt { Name = "OriginalName1", Content = "Content1", CreatedAt = DateTime.UtcNow, IsActive = true };
            var otherPrompt = new Prompt { Name = "ExistingName2", Content = "Content2", CreatedAt = DateTime.UtcNow, IsActive = true };
            dbContext.Prompts.AddRange(promptToUpdate, otherPrompt);
            await dbContext.SaveChangesAsync();
            dbContext.Entry(promptToUpdate).State = EntityState.Detached;
            dbContext.Entry(otherPrompt).State = EntityState.Detached;

            var request = new PromptRequest { Name = "ExistingName2", Content = "UpdatedContent", IsActive = true };

            // Act: The current implementation of UpdatePromptAsync does NOT check for duplicate names when updating.
            // It will simply update the name. If a unique constraint were on the Name property in the DB,
            // SaveChangesAsync would throw an exception. But the service method itself doesn't prevent it.
            // This test verifies the current behavior.
            var updatedPrompt = await service.UpdatePromptAsync(promptToUpdate.Id, request);

            // Assert
            Assert.NotNull(updatedPrompt);
            Assert.Equal("ExistingName2", updatedPrompt.Name);

            // To test for a unique name constraint violation, that would typically be an integration test
            // or if the service method explicitly checked:
            // var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdatePromptAsync(promptToUpdate.Id, request));
            // Assert.Equal($"Já existe um prompt com o nome '{request.Name}'", exception.Message);
        }


        [Fact]
        public async Task GetActivePromptContentAsync_WhenActivePromptExists_ShouldReturnItsContent()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);
            var promptName = "my_active_prompt";
            var expectedContent = "Active prompt content";

            dbContext.Prompts.Add(new Prompt { Name = promptName, Content = expectedContent, CreatedAt = DateTime.UtcNow, IsActive = true });
            dbContext.Prompts.Add(new Prompt { Name = promptName, Content = "Old inactive content", CreatedAt = DateTime.UtcNow.AddDays(-1), IsActive = false });
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetActivePromptContentAsync(promptName);

            // Assert
            Assert.Equal(expectedContent, result);
        }

        [Fact]
        public async Task GetActivePromptContentAsync_WhenMultipleActivePromptsExist_ShouldReturnLatestUpdatedOrCreated()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);
            var promptName = "multiple_active";
            var expectedContent = "Latest active content"; // This one is newer

            dbContext.Prompts.Add(new Prompt { Name = promptName, Content = "Older active content", CreatedAt = DateTime.UtcNow.AddHours(-2), IsActive = true });
            dbContext.Prompts.Add(new Prompt { Name = promptName, Content = expectedContent, CreatedAt = DateTime.UtcNow.AddHours(-1), IsActive = true, UpdatedAt = DateTime.UtcNow }); // Newest due to UpdatedAt
            dbContext.Prompts.Add(new Prompt { Name = promptName, Content = "Very old active content", CreatedAt = DateTime.UtcNow.AddHours(-3), IsActive = true });
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetActivePromptContentAsync(promptName);

            // Assert
            Assert.Equal(expectedContent, result);
        }
        
        [Fact]
        public async Task GetActivePromptContentAsync_WhenSpecificPromptNotActiveButDefaultExists_ShouldReturnDefaultContent()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object); // Default prompt name is "email_response"
            var specificPromptName = "specific_but_inactive";
            var defaultPromptName = "email_response"; // As per PromptService implementation
            var defaultContent = PromptService.DefaultPromptContent; // Accessing the const for verification

            dbContext.Prompts.Add(new Prompt { Name = specificPromptName, Content = "Specific inactive content", CreatedAt = DateTime.UtcNow, IsActive = false });
            dbContext.Prompts.Add(new Prompt { Name = defaultPromptName, Content = defaultContent, CreatedAt = DateTime.UtcNow.AddDays(-1), IsActive = true });
            await dbContext.SaveChangesAsync();

            // Act
            var result = await service.GetActivePromptContentAsync(specificPromptName);

            // Assert
            Assert.Equal(defaultContent, result);
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Prompt '{specificPromptName}' não encontrado. Usando prompt padrão.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task GetActivePromptContentAsync_WhenNeitherSpecificNorDefaultActiveExists_ShouldReturnHardcodedDefaultContent()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);
            var specificPromptName = "non_existent_prompt";
            var defaultContent = PromptService.DefaultPromptContent; // The hardcoded default

            // Ensure no "email_response" prompt is active or exists
            var existingDefault = await dbContext.Prompts.FirstOrDefaultAsync(p => p.Name == "email_response");
            if (existingDefault != null)
            {
                dbContext.Prompts.Remove(existingDefault);
                await dbContext.SaveChangesAsync();
            }

            // Act
            var result = await service.GetActivePromptContentAsync(specificPromptName);

            // Assert
            Assert.Equal(defaultContent, result);
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Warning,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains($"Prompt '{specificPromptName}' não encontrado. Usando prompt padrão.")),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EnsureDefaultPromptAsync_WhenDefaultPromptDoesNotExist_ShouldCreateIt()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);
            var defaultPromptName = "email_response";

            // Act
            await service.EnsureDefaultPromptAsync();

            // Assert
            var defaultPromptInDb = await dbContext.Prompts.FirstOrDefaultAsync(p => p.Name == defaultPromptName);
            Assert.NotNull(defaultPromptInDb);
            Assert.Equal(PromptService.DefaultPromptContent, defaultPromptInDb.Content);
            Assert.True(defaultPromptInDb.IsActive);
            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == "Prompt padrão criado com sucesso"),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EnsureDefaultPromptAsync_WhenDefaultPromptAlreadyExists_ShouldDoNothing()
        {
            // Arrange
            var dbContext = GetInMemoryDbContext();
            var loggerMock = GetLoggerMock();
            var service = new PromptService(dbContext, loggerMock.Object);
            var defaultPromptName = "email_response";

            dbContext.Prompts.Add(new Prompt { Name = defaultPromptName, Content = "Existing default content", CreatedAt = DateTime.UtcNow, IsActive = true });
            await dbContext.SaveChangesAsync();
            var initialCount = await dbContext.Prompts.CountAsync();

            // Act
            await service.EnsureDefaultPromptAsync();

            // Assert
            var finalCount = await dbContext.Prompts.CountAsync();
            Assert.Equal(initialCount, finalCount); // No new prompt should be added

            var defaultPromptInDb = await dbContext.Prompts.FirstOrDefaultAsync(p => p.Name == defaultPromptName);
            Assert.NotNull(defaultPromptInDb);
            Assert.Equal("Existing default content", defaultPromptInDb.Content); // Ensure it wasn't overwritten

            loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString() == "Prompt padrão criado com sucesso"),
                    null,
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Never); // Log de criação não deve ocorrer
        }
    }
}
