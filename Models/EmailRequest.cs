namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Modelo de requisição para processar e-mail
    /// </summary>
    public class EmailRequest
    {
        /// <summary>
        /// Conteúdo do e-mail a ser processado
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}