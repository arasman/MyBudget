using FluentValidation;

namespace MyBudget.Features.Features.CurrentSituation.DeleteCutRecord;

public sealed class DeleteCutRecordValidator : AbstractValidator<DeleteCutRecordCommand>
{
    public DeleteCutRecordValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");
    }
}
