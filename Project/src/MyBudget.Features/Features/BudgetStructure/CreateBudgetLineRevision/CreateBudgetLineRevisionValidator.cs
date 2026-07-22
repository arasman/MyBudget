using FluentValidation;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLineRevision;

/// <summary>
/// REQ-BLR-02: Validates CreateBudgetLineRevisionCommand.
/// - ValidFrom must be today or in the future.
/// - Amount must be greater than zero.
/// BudgetLine date-range guard is enforced in the handler (requires DB access).
/// </summary>
public sealed class CreateBudgetLineRevisionValidator
    : AbstractValidator<CreateBudgetLineRevisionCommand>
{
    public CreateBudgetLineRevisionValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.LineId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.ValidFrom)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED")
            .Must(d => d >= DateOnly.FromDateTime(DateTime.UtcNow))
                .WithErrorCode("REVISION_VALID_FROM_IN_PAST")
                .WithMessage("ValidFrom must be today or in the future.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithErrorCode("FIELD_INVALID");
    }
}
