// <copyright file="AuthControllerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using AzureChatGptMiddleware.Controllers;
using AzureChatGptMiddleware.Models;
using AzureChatGptMiddleware.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using Xunit;

namespace AzureChatGptMiddleware.Tests
{
    public class AuthControllerTests
    {
        private readonly Mock<ITokenService> _mockTokenService;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly Mock<ILogger<AuthController>> _mockLogger;

        public AuthControllerTests()
        {
            _mockTokenService = new Mock<ITokenService>();
            _mockConfiguration = new Mock<IConfiguration>();
            _mockLogger = new Mock<ILogger<AuthController>>();

            // Setup default ApiKey configuration for tests that might use it
            _mockConfiguration.Setup(c => c["ApiKeySettings:ApiKey"]).Returns("test-static-api-key");
        }

        private AuthController CreateController()
        {
            return new AuthController(
                _mockTokenService.Object,
                _mockConfiguration.Object,
                _mockLogger.Object
            );
        }

        [Fact]
        public void GenerateToken_WithValidRequestAndValidCredentials_ReturnsOkObjectResultWithToken()
        {
            // Arrange
            var request = new TokenRequest { Username = "admin", Password = "password" };
            var expectedToken = "generated-jwt-token";
            _mockTokenService.Setup(s => s.GenerateToken(request.Username, request.Password)) // Assuming GenerateToken takes username/password
                .Returns(expectedToken);
            var controller = CreateController();

            // Act
            var result = controller.GenerateToken(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var returnValue = okResult.Value;
            Assert.NotNull(returnValue);
            var tokenProperty = returnValue.GetType().GetProperty("token");
            var expirationProperty = returnValue.GetType().GetProperty("expiration"); // AuthController adds expiration

            Assert.NotNull(tokenProperty);
            Assert.Equal(expectedToken, tokenProperty.GetValue(returnValue, null));
            Assert.NotNull(expirationProperty); // Check that expiration is part of the response
        }

        [Fact]
        public void GenerateToken_WithNullRequest_ReturnsBadRequest()
        {
            // Arrange
            var controller = CreateController();

            // Act
            var result = controller.GenerateToken(null);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Requisição inválida.", badRequestResult.Value.GetType().GetProperty("message").GetValue(badRequestResult.Value, null));
        }

        [Theory]
        [InlineData(null, "password")]
        [InlineData("admin", null)]
        [InlineData("", "password")]
        [InlineData("admin", "")]
        public void GenerateToken_WithInvalidRequestData_ReturnsBadRequest(string username, string password)
        {
            // Arrange
            var request = new TokenRequest { Username = username, Password = password };
            var controller = CreateController();

            // Act
            var result = controller.GenerateToken(request);

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal("Nome de usuário e senha são obrigatórios.", badRequestResult.Value.GetType().GetProperty("message").GetValue(badRequestResult.Value, null));
        }

        [Fact]
        public void GenerateToken_WhenTokenServiceReturnsNull_ReturnsUnauthorized()
        {
            // Arrange
            var request = new TokenRequest { Username = "admin", Password = "wrongpassword" };
            _mockTokenService.Setup(s => s.GenerateToken(request.Username, request.Password))
                .Returns((string?)null); // Simulate invalid credentials
            var controller = CreateController();

            // Act
            var result = controller.GenerateToken(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal("Credenciais inválidas.", unauthorizedResult.Value.GetType().GetProperty("message").GetValue(unauthorizedResult.Value, null));
        }

        [Fact]
        public void GenerateToken_WhenTokenServiceThrowsException_ReturnsStatusCode500()
        {
            // Arrange
            var request = new TokenRequest { Username = "admin", Password = "password" };
            var exceptionMessage = "Token service error";
            _mockTokenService.Setup(s => s.GenerateToken(request.Username, request.Password))
                .Throws(new Exception(exceptionMessage));
            var controller = CreateController();

            // Act
            var result = controller.GenerateToken(request);

            // Assert
            var statusCodeResult = Assert.IsType<ObjectResult>(result);
            Assert.Equal(500, statusCodeResult.StatusCode);
            Assert.Contains(exceptionMessage, statusCodeResult.Value.GetType().GetProperty("message").GetValue(statusCodeResult.Value, null).ToString());
        }

        // Note: The AuthController provided in the file listing doesn't have an ApiKey endpoint.
        // If it did, tests for it would go here. For example:
        // [Fact]
        // public void GetApiKey_WithCorrectMasterKey_ReturnsOkWithApiKey()
        // {
        //     // Arrange
        //     var masterKey = "expectedMasterKey";
        //     _mockConfiguration.Setup(c => c["ApiKeySettings:MasterKey"]).Returns(masterKey);
        //     _mockConfiguration.Setup(c => c["ApiKeySettings:ApiKey"]).Returns("test-static-api-key");
        //     var controller = CreateController();
        //
        //     // Act
        //     var result = controller.GetApiKey(masterKey);
        //
        //     // Assert
        //     var okResult = Assert.IsType<OkObjectResult>(result);
        //     Assert.NotNull(okResult.Value);
        //     // Add more assertions based on the expected response structure
        // }
        //
        // [Fact]
        // public void GetApiKey_WithIncorrectMasterKey_ReturnsUnauthorized()
        // {
        //     // Arrange
        //     var masterKey = "expectedMasterKey";
        //     _mockConfiguration.Setup(c => c["ApiKeySettings:MasterKey"]).Returns(masterKey);
        //     var controller = CreateController();
        //
        //     // Act
        //     var result = controller.GetApiKey("incorrectMasterKey");
        //
        //     // Assert
        //     Assert.IsType<UnauthorizedResult>(result);
        // }
    }
}
