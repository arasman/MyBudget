using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.UpdatePeriod;

public sealed class UpdatePeriodValidator : AbstractValidator<UpdatePeriodCommand>
{
    public UpdatePeriodValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CycleId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.PeriodId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(200).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.PeriodNumber)
            .GreaterThan(0).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.StartDate)
            .Must((cmd, start) => start < cmd.EndDate)
            .WithErrorCode("PERIOD_INVALID_DATE_RANGE")
            .WithMessage("StartDate must be before EndDate.");
    }
}
