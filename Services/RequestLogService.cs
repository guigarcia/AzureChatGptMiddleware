using AzureChatGptMiddleware.Data;
using AzureChatGptMiddleware.Data.Entities;

namespace AzureChatGptMiddleware.Services
{
    public class RequestLogService : IRequestLogService
    {
        private readonly ApplicationDbContext _context;

        public RequestLogService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<RequestLog> LogRequestAsync(string input, string output, bool success, string? errorMessage = null, string? clientInfo = null)
        {
            var log = new RequestLog
            {
                Input = input,
                Output = output,
                Success = success,
                ErrorMessage = errorMessage,
                ClientInfo = clientInfo,
                CreatedAt = DateTime.UtcNow
            };

            _context.RequestLogs.Add(log);
            await _context.SaveChangesAsync();

            return log;
        }
    }
}