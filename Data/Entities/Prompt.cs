namespace AzureChatGptMiddleware.Data.Entities
{
    /// <summary>
    /// Entidade para armazenar prompts do sistema
    /// </summary>
    public class Prompt
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}