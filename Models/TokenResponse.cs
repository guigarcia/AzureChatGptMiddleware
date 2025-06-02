namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Modelo de resposta com token JWT
    /// </summary>
    public class TokenResponse
    {
        /// <summary>
        /// Token JWT gerado
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Data de expiração do token
        /// </summary>
        public DateTime ExpiresAt { get; set; }
    }
}