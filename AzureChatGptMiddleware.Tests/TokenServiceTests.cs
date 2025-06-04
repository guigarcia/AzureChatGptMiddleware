using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using AzureChatGptMiddleware.Services;
using Xunit;

namespace AzureChatGptMiddleware.Tests;

public class TokenServiceTests
{
    [Fact]
    public void GenerateToken_ShouldReturnToken_WithConfiguredExpiration()
    {
        // Arrange
        var expirationMinutes = 30;
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"JwtSettings:SecretKey", "mysecretkeymysecretkeymysecretkey"},
            {"JwtSettings:Issuer", "TestIssuer"},
            {"JwtSettings:Audience", "TestAudience"},
            {"JwtSettings:ExpirationMinutes", expirationMinutes.ToString()}
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings!)
            .Build();
        var tokenService = new TokenService(configuration);
        var now = DateTime.UtcNow;

        // Act
        var token = tokenService.GenerateToken();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        var diff = jwt.ValidTo - now;
        Assert.InRange(diff.TotalMinutes, expirationMinutes - 0.5, expirationMinutes + 0.5);
    }
}
