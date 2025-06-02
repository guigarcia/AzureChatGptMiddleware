namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Representa a requisição para processamento de um e-mail.
    /// As validações são definidas em EmailRequestValidator.
    /// </summary>
    public class EmailRequest
    {
        /// <summary>
        /// O conteúdo textual do e-mail que precisa ser processado pela IA.
        /// Restrições: Obrigatório, máximo de 10000 caracteres.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}