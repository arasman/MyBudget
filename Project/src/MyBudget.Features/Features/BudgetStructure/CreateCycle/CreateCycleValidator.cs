using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.CreateCycle;

public sealed class CreateCycleValidator : AbstractValidator<CreateCycleCommand>
{
    public CreateCycleValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .MaximumLength(200).WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.StartDate)
            .Must((cmd, start) => start < cmd.EndDate)
            .WithErrorCode("CYCLE_INVALID_DATE_RANGE")
            .WithMessage("StartDate must be before EndDate.");

        RuleFor(x => x.DefaultCurrencyId)
            .NotEmpty().WithErrorCode("CYC_DEFAULT_CURRENCY_REQUIRED");

        RuleFor(x => x)
            .Must(cmd => cmd.AlternateCurrencyId.HasValue == cmd.ExchangeRate.HasValue)
            .WithErrorCode("CYC_PAIR_INCOMPLETE")
            .WithMessage("AlternateCurrencyId and ExchangeRate must both be provided or both omitted.")
            .WithName("AlternateCurrencyId");
    }
}
