// <copyright file="TokenServiceTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using AzureChatGptMiddleware.Models;
using AzureChatGptMiddleware.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Xunit;

namespace AzureChatGptMiddleware.Tests
{
    public class TokenServiceTests
    {
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly string _defaultSecretKey = "TestSuperSecretKeyForTestingPurposeWithSufficientLength"; // Min 32 chars for HS256
        private readonly string _defaultIssuer = "TestIssuer";
        private readonly string _defaultAudience = "TestAudience";
        private readonly double _defaultExpirationMinutes = 30;

        public TokenServiceTests()
        {
            _mockConfiguration = new Mock<IConfiguration>();
            SetupDefaultJwtConfiguration();
        }

        private void SetupDefaultJwtConfiguration()
        {
            var jwtSettingsSection = new Mock<IConfigurationSection>();
            jwtSettingsSection.Setup(x => x["SecretKey"]).Returns(_defaultSecretKey);
            jwtSettingsSection.Setup(x => x["Issuer"]).Returns(_defaultIssuer);
            jwtSettingsSection.Setup(x => x["Audience"]).Returns(_defaultAudience);
            jwtSettingsSection.Setup(x => x["ExpirationMinutes"]).Returns(_defaultExpirationMinutes.ToString());

            _mockConfiguration.Setup(x => x.GetSection("JwtSettings")).Returns(jwtSettingsSection.Object);

            // For ApiKey validation part (though not directly tested in GenerateToken for user/pass)
            var apiKeySection = new Mock<IConfigurationSection>();
            apiKeySection.Setup(x => x["Value"]).Returns("TestApiKey");
            _mockConfiguration.Setup(x => x.GetSection("ApiKey")).Returns(apiKeySection.Object);
        }

        private TokenService CreateService()
        {
            return new TokenService(_mockConfiguration.Object);
        }

        [Fact]
        public void GenerateToken_WithValidAdminCredentials_ShouldReturnValidJwtToken()
        {
            // Arrange
            var service = CreateService();
            // The current TokenService.GenerateToken() doesn't take TokenRequest.
            // It seems the request was based on an older version or a misunderstanding.
            // The current implementation generates a token without validating user credentials.
            // I will test the current implementation.

            // Act
            var tokenString = service.GenerateToken();

            // Assert
            Assert.NotNull(tokenString);
            Assert.NotEmpty(tokenString);

            var handler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_defaultSecretKey)),
                ValidateIssuer = true,
                ValidIssuer = _defaultIssuer,
                ValidateAudience = true,
                ValidAudience = _defaultAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero // Important for expiration check
            };

            SecurityToken validatedToken;
            var principal = handler.ValidateToken(tokenString, validationParameters, out validatedToken);
            var jwtToken = validatedToken as JwtSecurityToken;

            Assert.NotNull(jwtToken);
            Assert.Equal(_defaultIssuer, jwtToken.Issuer);
            Assert.Equal(_defaultAudience, jwtToken.Audiences.FirstOrDefault());

            // Check claims
            Assert.True(jwtToken.Claims.Any(c => c.Type == ClaimTypes.Name && c.Value == "HughesClient"));
            Assert.True(jwtToken.Claims.Any(c => c.Type == ClaimTypes.Role && c.Value == "ApiUser"));
            Assert.True(jwtToken.Claims.Any(c => c.Type == "client_id")); // client_id is a new Guid

            // Check expiration (approximate)
            var expectedExpiration = DateTime.UtcNow.AddMinutes(_defaultExpirationMinutes);
            Assert.True(jwtToken.ValidTo > DateTime.UtcNow.AddMinutes(_defaultExpirationMinutes - 1) &&
                        jwtToken.ValidTo < DateTime.UtcNow.AddMinutes(_defaultExpirationMinutes + 1));
        }

        // Based on the current TokenService, it doesn't validate credentials for GenerateToken.
        // If it were to validate, these tests would be relevant.
        // [Fact]
        // public void GenerateToken_WithInvalidUsername_ShouldReturnNull()
        // {
        //     // Arrange
        //     var service = CreateService();
        //     var request = new TokenRequest { Username = "notadmin", Password = "password" };
        //
        //     // Act
        //     var tokenString = service.GenerateToken(request); // Assuming GenerateToken takes TokenRequest
        //
        //     // Assert
        //     Assert.Null(tokenString);
        // }
        //
        // [Fact]
        // public void GenerateToken_WithInvalidPassword_ShouldReturnNull()
        // {
        //     // Arrange
        //     var service = CreateService();
        //     var request = new TokenRequest { Username = "admin", Password = "wrongpassword" };
        //
        //     // Act
        //     var tokenString = service.GenerateToken(request); // Assuming GenerateToken takes TokenRequest
        //
        //     // Assert
        //     Assert.Null(tokenString);
        // }

        [Fact]
        public void GenerateToken_WhenSecretKeyIsMissing_ShouldThrowInvalidOperationException()
        {
            // Arrange
            var jwtSettingsSection = new Mock<IConfigurationSection>();
            jwtSettingsSection.Setup(x => x["SecretKey"]).Returns((string?)null); // Missing SecretKey
            jwtSettingsSection.Setup(x => x["Issuer"]).Returns(_defaultIssuer);
            jwtSettingsSection.Setup(x => x["Audience"]).Returns(_defaultAudience);
            jwtSettingsSection.Setup(x => x["ExpirationMinutes"]).Returns(_defaultExpirationMinutes.ToString());
            _mockConfiguration.Setup(x => x.GetSection("JwtSettings")).Returns(jwtSettingsSection.Object);

            var service = CreateService();

            // Act & Assert
            var ex = Assert.Throws<InvalidOperationException>(() => service.GenerateToken());
            Assert.Equal("JWT SecretKey não configurada", ex.Message);
        }

        [Fact]
        public void ValidateApiKey_WithValidKey_ReturnsTrue()
        {
            // Arrange
            var service = CreateService();
            var configuredApiKey = "TestApiKey"; // Must match what's in SetupDefaultJwtConfiguration
            _mockConfiguration.Setup(x => _mockConfiguration.Object["ApiKey:Value"]).Returns(configuredApiKey);


            // Act
            var result = service.ValidateApiKey(configuredApiKey);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void ValidateApiKey_WithInvalidKey_ReturnsFalse()
        {
            // Arrange
            var service = CreateService();
            var configuredApiKey = "TestApiKey";
             _mockConfiguration.Setup(x => _mockConfiguration.Object["ApiKey:Value"]).Returns(configuredApiKey);

            // Act
            var result = service.ValidateApiKey("InvalidKey");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void ValidateApiKey_WithNullOrEmptyKey_ReturnsFalse()
        {
            // Arrange
            var service = CreateService();
             var configuredApiKey = "TestApiKey";
            _mockConfiguration.Setup(x => _mockConfiguration.Object["ApiKey:Value"]).Returns(configuredApiKey);


            // Act
            var resultNull = service.ValidateApiKey(null);
            var resultEmpty = service.ValidateApiKey(string.Empty);

            // Assert
            Assert.False(resultNull);
            Assert.False(resultEmpty);
        }

        [Fact]
        public void ValidateApiKey_WhenConfiguredKeyIsMissing_ReturnsFalseForAnyKey()
        {
            // Arrange
             _mockConfiguration.Setup(x => _mockConfiguration.Object["ApiKey:Value"]).Returns((string?)null);
            var service = CreateService();

            // Act
            var result = service.ValidateApiKey("SomeKeyInput");

            // Assert
            Assert.False(result);
        }
    }
}
