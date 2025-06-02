using AzureChatGptMiddleware.Data.Entities;
using AzureChatGptMiddleware.Models;

namespace AzureChatGptMiddleware.Services
{
    public interface IPromptService
    {
        Task<string> GetActivePromptContentAsync(string name);
        Task<Prompt> CreatePromptAsync(PromptRequest request);
        Task<Prompt> UpdatePromptAsync(int id, PromptRequest request);
        Task<List<PromptResponse>> GetAllPromptsAsync();
        Task<PromptResponse?> GetPromptByIdAsync(int id);
        Task EnsureDefaultPromptAsync();
    }
}