using FluentValidation;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;

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

        // REQ-BL-03: Amount revision — only validated when present
        RuleFor(x => x.BudgetedAmount)
            .GreaterThan(0).WithErrorCode("FIELD_INVALID")
            .When(x => x.BudgetedAmount.HasValue);

        // REQ-BL-03: ValidFrom must not be in the past (no retroactive revision splits)
        RuleFor(x => x.ValidFrom)
            .Must(vf => vf!.Value >= DateOnly.FromDateTime(DateTime.UtcNow))
            .WithErrorCode("VALIDFROM_IN_PAST")
            .When(x => x.ValidFrom.HasValue);

        // REQ-BL-03: ValidTo when provided must be >= ValidFrom
        RuleFor(x => x.ValidTo)
            .Must((cmd, validTo) => validTo!.Value >= cmd.ValidFrom!.Value)
            .WithErrorCode("FIELD_INVALID")
            .When(x => x.ValidFrom.HasValue && x.ValidTo.HasValue);
    }
}
