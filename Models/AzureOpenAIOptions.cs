using System.ComponentModel.DataAnnotations;

namespace AzureChatGptMiddleware.Models
{
    /// <summary>
    /// Contém as configurações necessárias para interagir com o serviço Azure OpenAI.
    /// Estas configurações são carregadas da seção "AzureOpenAI" do appsettings.json
    /// e validadas na inicialização da aplicação.
    /// </summary>
    public class AzureOpenAIOptions
    {
        public const string SectionName = "AzureOpenAI";

        /// <summary>
        /// O Endpoint da API do recurso Azure OpenAI.
        /// Exemplo: "https://seu-recurso.openai.azure.com/"
        /// </summary>
        [Required(ErrorMessage = "Azure OpenAI Endpoint é obrigatório.")]
        [Url(ErrorMessage = "Azure OpenAI Endpoint deve ser uma URL válida.")]
        public string Endpoint { get; set; } = string.Empty;

        /// <summary>
        /// A chave de API para autenticação com o serviço Azure OpenAI.
        /// </summary>
        [Required(ErrorMessage = "Azure OpenAI ApiKey é obrigatório.")]
        public string ApiKey { get; set; } = string.Empty;

        /// <summary>
        /// O nome do modelo (deployment) a ser utilizado no Azure OpenAI.
        /// Exemplo: "gpt-35-turbo", "text-davinci-003"
        /// </summary>
        [Required(ErrorMessage = "Azure OpenAI DeploymentName é obrigatório.")]
        public string DeploymentName { get; set; } = string.Empty;

        /// <summary>
        /// A versão da API do Azure OpenAI a ser utilizada.
        /// Exemplo: "2024-02-01", "2023-05-15"
        /// </summary>
        [Required(ErrorMessage = "Azure OpenAI ApiVersion é obrigatório.")]
        public string ApiVersion { get; set; } = string.Empty;
    }
}
