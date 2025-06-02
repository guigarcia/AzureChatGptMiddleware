namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Modelo de requisição para criar/atualizar prompt
    /// </summary>
    public class PromptRequest
    {
        /// <summary>
        /// Nome identificador do prompt
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Conteúdo do prompt
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Se o prompt está ativo
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}