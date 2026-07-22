using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.DeleteBudgetLine;

public sealed class DeleteBudgetLineValidator : AbstractValidator<DeleteBudgetLineCommand>
{
    public DeleteBudgetLineValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.LineId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
