namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Modelo de resposta do processamento de e-mail
    /// </summary>
    public class EmailResponse
    {
        /// <summary>
        /// Resposta gerada pela IA
        /// </summary>
        public string Response { get; set; } = string.Empty;

        /// <summary>
        /// ID da requisição no banco de dados
        /// </summary>
        public int RequestId { get; set; }

        /// <summary>
        /// Data e hora do processamento
        /// </summary>
        public DateTime ProcessedAt { get; set; }
    }
}