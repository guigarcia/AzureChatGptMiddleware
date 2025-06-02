namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Representa a resposta da autenticação bem-sucedida, contendo o token JWT.
    /// </summary>
    public class TokenResponse
    {
        /// <summary>
        /// O token JWT (JSON Web Token) gerado para o cliente autenticado.
        /// Este token deve ser usado no header "Authorization" (como Bearer token) para acessar endpoints protegidos.
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// A data e hora exatas (em UTC) em que o token JWT irá expirar.
        /// </summary>
        public DateTime ExpiresAt { get; set; }
    }
}