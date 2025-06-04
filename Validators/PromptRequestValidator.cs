using AzureChatGptMiddleware.Models;
using FluentValidation;

namespace AzureChatGptMiddleware.Validators
{
  
    public class PromptRequestValidator : AbstractValidator<PromptRequest>
    {
        public PromptRequestValidator()
        {
            // Regra para a propriedade Name:
            // - Não pode ser nulo ou vazio.
            // - Deve ter no máximo 100 caracteres.
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("O nome do prompt é obrigatório.")
                .MaximumLength(100).WithMessage("O nome do prompt não pode exceder 100 caracteres.");

            // Regra para a propriedade Content:
            // - Não pode ser nulo ou vazio.
            // - Deve ter no máximo 4000 caracteres.
            RuleFor(x => x.Content)
                .NotEmpty().WithMessage("O conteúdo do prompt é obrigatório.")
                .MaximumLength(4000).WithMessage("O conteúdo do prompt não pode exceder 4000 caracteres.");
            
            // A propriedade IsActive (booleana) não requer validação explícita aqui,
            // pois seu tipo já garante que será true ou false.
        }
    }
}
