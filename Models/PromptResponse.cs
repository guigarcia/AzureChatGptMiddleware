namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Representa os dados de um prompt do sistema, conforme retornado pela API.
    /// </summary>
    public class PromptResponse
    {
        /// <summary>
        /// O ID único do prompt no banco de dados.
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Nome único identificador do prompt.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// O conteúdo textual completo do prompt.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Indica se o prompt está atualmente ativo.
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// A data e hora (em UTC) em que o prompt foi criado.
        /// </summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// A data e hora (em UTC) da última atualização do prompt.
        /// Pode ser nulo se o prompt nunca foi atualizado.
        /// </summary>
        public DateTime? UpdatedAt { get; set; }
    }
}