namespace AzureChatGptMiddleware.Data.Entities
{
    /// <summary>
    /// Entidade para armazenar logs de requisições
    /// </summary>
    public class RequestLog
    {
        public int Id { get; set; }
        public string Input { get; set; } = string.Empty;
        public string Output { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? ClientInfo { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }
}