using FluentValidation;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;

public sealed class CreateBudgetLineValidator : AbstractValidator<CreateBudgetLineCommand>
{
    public CreateBudgetLineValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.PeriodId)
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
            .GreaterThan(0).WithErrorCode("FIELD_INVALID"); // REQ-BL-AMOUNT-1: amount must be > 0
    }
}
