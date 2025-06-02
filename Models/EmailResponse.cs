namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Representa a resposta do processamento de um e-mail pela IA.
    /// </summary>
    public class EmailResponse
    {
        /// <summary>
        /// O texto da resposta gerado pela IA para o e-mail de entrada.
        /// </summary>
        public string Response { get; set; } = string.Empty;

        /// <summary>
        /// O ID único do registro de log para esta requisição/resposta específica,
        /// conforme armazenado no banco de dados.
        /// </summary>
        public int RequestId { get; set; }

        /// <summary>
        /// A data e hora (em UTC) em que o processamento do e-mail foi concluído.
        /// </summary>
        public DateTime ProcessedAt { get; set; }
    }
}