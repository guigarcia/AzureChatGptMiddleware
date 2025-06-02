namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Modelo de resposta com dados do prompt
    /// </summary>
    public class PromptResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}