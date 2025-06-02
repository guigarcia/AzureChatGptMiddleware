namespace AzureChatGptMiddleware.Services
{
    public interface ITokenService
    {
        string GenerateToken();
        bool ValidateApiKey(string apiKey);
    }
}