namespace AzureChatGptMiddleware.Services
{
    public interface IAzureOpenAIService
    {
        Task<string> ProcessEmailAsync(string emailContent);
    }
}