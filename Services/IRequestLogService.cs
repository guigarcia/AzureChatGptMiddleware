using AzureChatGptMiddleware.Data.Entities;

namespace AzureChatGptMiddleware.Services
{
    public interface IRequestLogService
    {
        Task<RequestLog> LogRequestAsync(string input, string output, bool success, string? errorMessage = null, string? clientInfo = null);
    }
}
