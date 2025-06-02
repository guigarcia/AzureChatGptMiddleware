using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace AzureChatGptMiddleware.Services
{
    /// <summary>
    /// Serviço responsável pela geração de tokens JWT e validação de API Keys.
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _configuration;

        /// <summary>
        /// Inicializa uma nova instância da classe <see cref="TokenService"/>.
        /// </summary>
        /// <param name="configuration">A configuração da aplicação, usada para obter as configurações do JWT e da API Key.</param>
        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Gera um token JWT.
        /// Atualmente, este método não valida credenciais de usuário/senha diretamente.
        /// A validação de credenciais (ex: "admin"/"password") é esperada ter ocorrido
        /// no AuthController antes de chamar este método. Este método apenas constrói o token
        /// com claims predefinidas se a lógica de negócios no controller permitir sua chamada.
        /// As configurações para o token (SecretKey, Issuer, Audience, ExpirationMinutes)
        /// são lidas da seção "JwtSettings" do IConfiguration.
        /// </summary>
        /// <returns>Uma string representando o token JWT gerado.</returns>
        /// <exception cref="InvalidOperationException">Lançada se a SecretKey JWT não estiver configurada.</exception>
        public string GenerateToken()
        {
            // Carrega as configurações do JWT da seção "JwtSettings" (ex: appsettings.json)
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var secretKeyString = jwtSettings["SecretKey"];
            if (string.IsNullOrEmpty(secretKeyString))
            {
                // Falha rápida se a chave secreta não estiver configurada.
                throw new InvalidOperationException("JWT SecretKey não configurada");
            }
            var secretKey = Encoding.ASCII.GetBytes(secretKeyString);
            
            var tokenHandler = new JwtSecurityTokenHandler();

            // Define as claims para o token. Atualmente são fixas.
            // O "client_id" é gerado unicamente para cada token, o que pode não ser o comportamento usual
            // para um client_id, mas serve como um identificador único para o token (similar a jti).
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.Name, "HughesClient"), // Nome genérico para o cliente/usuário do token
                    new Claim(ClaimTypes.Role, "ApiUser"),      // Papel associado ao token
                    new Claim("client_id", Guid.NewGuid().ToString()) // Identificador único para este token específico
                }),
                Expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpirationMinutes"])), // Tempo de expiração do token
                Issuer = jwtSettings["Issuer"], // Emissor do token
                Audience = jwtSettings["Audience"], // Destinatário do token
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256Signature) // Credenciais de assinatura
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        /// <summary>
        /// Valida uma API Key fornecida contra a API Key configurada na aplicação.
        /// A API Key configurada é lida de "ApiKey:Value" no IConfiguration.
        /// </summary>
        /// <param name="apiKey">A API Key a ser validada.</param>
        /// <returns>True se a API Key fornecida for válida; caso contrário, false.</returns>
        public bool ValidateApiKey(string apiKey)
        {
            var configuredApiKey = _configuration["ApiKey:Value"]; // Lê de "ApiKey:Value"
            return !string.IsNullOrEmpty(apiKey) && apiKey == configuredApiKey;
        }
    }
}
