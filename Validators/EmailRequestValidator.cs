using AzureChatGptMiddleware.Models;
using FluentValidation;

namespace AzureChatGptMiddleware.Validators
{
  
    public class EmailRequestValidator : AbstractValidator<EmailRequest>
    {
        public EmailRequestValidator()
        {
            // Regra para a propriedade Message (conteúdo do e-mail):
            // - Não pode ser nulo ou vazio.
            // - Deve ter no máximo 10000 caracteres.
            RuleFor(x => x.Message)
                .NotEmpty().WithMessage("O conteúdo do e-mail é obrigatório.")
                .MaximumLength(10000).WithMessage("O conteúdo do e-mail não pode exceder 10000 caracteres.");
        }
    }
}
