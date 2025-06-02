namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Representa os dados necessários para solicitar um token JWT utilizando uma API Key.
    /// </summary>
    public class TokenRequest
    {
        /// <summary>
        /// A API Key do cliente que está solicitando um token JWT.
        /// Esta chave é validada antes da emissão do token.
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;
    }
}