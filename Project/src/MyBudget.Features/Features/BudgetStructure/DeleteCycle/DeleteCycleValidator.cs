using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCycle;

public sealed class DeleteCycleValidator : AbstractValidator<DeleteCycleCommand>
{
    public DeleteCycleValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CycleId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
