using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace AzureChatGptMiddleware.Middleware
{
    /// <summary>
    /// Middleware para autenticação baseada em API Key.
    /// Este middleware verifica a presença de uma API Key no header "X-API-Key"
    /// para proteger os endpoints da aplicação.
    /// </summary>
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IConfiguration _configuration;
        private const string ApiKeyHeaderName = "X-API-Key"; // Nome do header para a API Key.

        public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
        {
            _next = next;
            _configuration = configuration;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 1. Pular validação para endpoints públicos.
            // Endpoints como Swagger UI e o endpoint de autenticação para obter token JWT
            // não devem ser protegidos por este middleware de API Key.
            if (context.Request.Path.StartsWithSegments("/swagger") ||
                context.Request.Path.StartsWithSegments("/api/auth/token")) // Endpoint de geração de token JWT.
            {
                await _next(context);
                return;
            }

            // 2. Priorizar autenticação JWT, se presente.
            // Se o header "Authorization" (usado para tokens JWT Bearer) estiver presente,
            // assume-se que a autenticação JWT será tratada pelo seu respectivo middleware.
            // Portanto, este middleware de API Key não interfere.
            if (context.Request.Headers.ContainsKey("Authorization"))
            {
                await _next(context);
                return;
            }

            // 3. Validar API Key.
            // Tenta extrair a API Key do header "X-API-Key".
            if (!context.Request.Headers.TryGetValue(ApiKeyHeaderName, out var extractedApiKey))
            {
                // Se o header não for encontrado, retorna 401 Unauthorized.
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("API Key não fornecida no header " + ApiKeyHeaderName);
                return;
            }

            // Obtém a API Key configurada na aplicação (ex: appsettings.json, Key Vault).
            var configuredApiKey = _configuration["ApiKey:Value"];

            // Compara a API Key extraída com a API Key configurada.
            // É importante que a comparação seja segura (embora string.Equals seja geralmente ok para chaves longas).
            if (string.IsNullOrEmpty(configuredApiKey) || !configuredApiKey.Equals(extractedApiKey))
            {
                // Se as chaves não corresponderem ou a chave configurada estiver ausente, retorna 401 Unauthorized.
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("API Key inválida.");
                return;
            }

            // Se a API Key for válida, permite que a requisição prossiga para o próximo middleware no pipeline.
            await _next(context);
        }
    }
}