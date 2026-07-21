using FluentValidation;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;

// TODO PR2a: add ValidFrom >= today, ValidFrom within BudgetLine date range, BudgetedAmount > 0 when provided
public sealed class UpdateBudgetLineValidator : AbstractValidator<UpdateBudgetLineCommand>
{
    public UpdateBudgetLineValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.LineId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CategoryGroupId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(200).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.LineType)
            .Must(lt => Enum.IsDefined(typeof(LineType), lt))
            .WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.BudgetedAmount)
            .GreaterThan(0).WithErrorCode("FIELD_INVALID")
            .When(x => x.BudgetedAmount.HasValue);
    }
}
