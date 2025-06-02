namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Modelo de requisição para obter token JWT
    /// </summary>
    public class TokenRequest
    {
        /// <summary>
        /// Chave de API para autenticação
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
    }
}