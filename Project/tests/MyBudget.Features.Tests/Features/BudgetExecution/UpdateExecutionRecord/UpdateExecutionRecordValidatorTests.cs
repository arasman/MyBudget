using MyBudget.Features.Features.BudgetExecution.UpdateExecutionRecord;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetExecution.UpdateExecutionRecord;

public sealed class UpdateExecutionRecordValidatorTests
{
    private readonly UpdateExecutionRecordValidator _sut = new();

    private static UpdateExecutionRecordCommand ValidCommand() =>
        new(
            BudgetId:        Guid.NewGuid(),
            PeriodId:        Guid.NewGuid(),
            BudgetLineId:    Guid.NewGuid(),
            ExecutionId:     Guid.NewGuid(),
            EntryType:       EntryType.Expense,
            Amount:          100m,
            Note:            "required note", // REQ-EXEC-4: Note is now always required
            CurrencyId:      CurrencySeeds.GtqId,
            ExchangeRate:    null,
            ExchangeRateTo:  null,
            AccountId:       null,
            PaymentMethodId: null);

    [Fact]
    public void Amount_Zero_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { Amount = 0m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "AMOUNT_MUST_BE_POSITIVE");
    }

    [Fact]
    public void Amount_Negative_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { Amount = -10m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "AMOUNT_MUST_BE_POSITIVE");
    }

    [Fact]
    public void Amount_Positive_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { Amount = 50m });
        result.IsValid.ShouldBeTrue();
    }

    // REQ-EXEC-4: Note required for ALL entry types; error code is NOTE_REQUIRED

    [Fact]
    public void Note_Absent_For_CreditNote_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.CreditNote, Note = null });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "NOTE_REQUIRED");
    }

    [Fact]
    public void Note_Empty_For_DebitNote_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.DebitNote, Note = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "NOTE_REQUIRED");
    }

    [Fact]
    public void Note_Absent_For_Expense_Rejected()
    {
        // REQ-EXEC-4: Note is now required for Expense too (changed from optional)
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.Expense, Note = null });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "NOTE_REQUIRED");
    }

    [Fact]
    public void Note_Present_For_Expense_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.Expense, Note = "grocery run" });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Note_Present_For_CreditNote_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.CreditNote, Note = "refund" });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ExecutionId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { ExecutionId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateExecutionRecordCommand.ExecutionId));
    }

    [Fact]
    public void ValidPayload_Passes()
    {
        var result = _sut.Validate(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }
}
