using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.ReorderBudgetLines;

public sealed class ReorderBudgetLinesValidator : AbstractValidator<ReorderBudgetLinesCommand>
{
    public ReorderBudgetLinesValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.PeriodId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.OrderedIds)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .Must(ids => ids.Distinct().Count() == ids.Length)
            .WithErrorCode("REORDER_DUPLICATE_ID");
    }
}
