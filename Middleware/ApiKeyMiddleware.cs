using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AzureChatGptMiddleware.Middleware
{
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Pular validação para endpoints que não requerem API Key
            if (context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/api/auth/token"))
            {
                await _next(context);
                return;
            }

            // Verificar se tem Authorization header (JWT)
            if (context.Request.Headers.ContainsKey("Authorization"))
            {
                await _next(context);
                return;
            }

            // Verificar API Key
            if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API Key não fornecida");
                return;
            }

            var apiKey = _configuration["ApiKey:Value"];
            if (!apiKey.Equals(extractedApiKey))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsync("API Key inválida");
                return;
            }

            await _next(context);
        }
    }
}