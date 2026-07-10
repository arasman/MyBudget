using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.DeletePeriod;

public sealed class DeletePeriodValidator : AbstractValidator<DeletePeriodCommand>
{
    public DeletePeriodValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CycleId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.PeriodId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
