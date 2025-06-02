namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Representa os dados para criar ou atualizar um prompt do sistema.
    /// As validações são definidas em PromptRequestValidator.
    /// </summary>
    public class PromptRequest
    {
        /// <summary>
        /// Nome único identificador do prompt.
        /// Exemplo: "email_response_formal", "email_response_informal".
        /// Restrições: Obrigatório, máximo de 100 caracteres.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// O conteúdo textual completo do prompt a ser utilizado pela IA.
        /// Restrições: Obrigatório, máximo de 4000 caracteres.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Indica se o prompt está ativo e pode ser selecionado para uso.
        /// Apenas um prompt com um determinado nome deve ser considerado ativo por vez (lógica controlada no serviço).
        /// O valor padrão é `true` ao criar um novo prompt.
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}