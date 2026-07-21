using FluentValidation;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.Features.BudgetExecution.UpdateExecutionRecord;

public sealed class UpdateExecutionRecordValidator : AbstractValidator<UpdateExecutionRecordCommand>
{
    public UpdateExecutionRecordValidator()
    {
        RuleFor(x => x.BudgetId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.PeriodId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.BudgetLineId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.ExecutionId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.CurrencyId)
            .NotEmpty().WithErrorCode("FIELD_REQUIRED");

        RuleFor(x => x.EntryType)
            .Must(et => Enum.IsDefined(typeof(EntryType), et))
            .WithErrorCode("FIELD_INVALID");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithErrorCode("AMOUNT_MUST_BE_POSITIVE");

        // REQ-EXEC-4: Note is required for ALL entry types
        RuleFor(x => x.Note)
            .NotEmpty()
            .WithErrorCode("NOTE_REQUIRED");
    }
}
