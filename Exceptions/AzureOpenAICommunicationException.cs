using System;
using System.Net;

namespace AzureChatGptMiddleware.Exceptions
{
    /// <summary>
    /// Exceção lançada quando ocorrem erros na comunicação com a API do Azure OpenAI.
    /// Isso pode incluir erros de rede, timeouts, status codes de erro HTTP da API,
    /// ou problemas ao processar a resposta da API (ex: JSON malformado).
    /// </summary>
    public class AzureOpenAICommunicationException : Exception
    {
        /// <summary>
        /// O código de status HTTP retornado pela API do Azure OpenAI, se aplicável.
        /// </summary>
        public HttpStatusCode? StatusCode { get; }

        /// <summary>
        /// O conteúdo da resposta de erro retornado pela API do Azure OpenAI, se disponível.
        /// </summary>
        public string? ErrorResponseContent { get; }

        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="AzureOpenAICommunicationException"/>.
        /// </summary>
        /// <param name="message">A mensagem que descreve o erro.</param>
        public AzureOpenAICommunicationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="AzureOpenAICommunicationException"/>.
        /// </summary>
        /// <param name="message">A mensagem que descreve o erro.</param>
        /// <param name="innerException">A exceção que é a causa da exceção atual.</param>
        public AzureOpenAICommunicationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="AzureOpenAICommunicationException"/>.
        /// </summary>
        /// <param name="message">A mensagem que descreve o erro.</param>
        /// <param name="statusCode">O código de status HTTP retornado pela API.</param>
        /// <param name="errorResponseContent">O conteúdo da resposta de erro da API.</param>
        /// <param name="innerException">A exceção que é a causa da exceção atual (opcional).</param>
        public AzureOpenAICommunicationException(
            string message,
            HttpStatusCode statusCode,
            string? errorResponseContent = null,
            Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            ErrorResponseContent = errorResponseContent;
        }
    }
}
