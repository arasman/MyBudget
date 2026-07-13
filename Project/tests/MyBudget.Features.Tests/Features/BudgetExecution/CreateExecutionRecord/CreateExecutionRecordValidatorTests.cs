using MyBudget.Features.Features.BudgetExecution.CreateExecutionRecord;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetExecution.CreateExecutionRecord;

public sealed class CreateExecutionRecordValidatorTests
{
    private readonly CreateExecutionRecordValidator _sut = new();

    private static CreateExecutionRecordCommand ValidCommand() =>
        new(
            BudgetId:        Guid.NewGuid(),
            PeriodId:        Guid.NewGuid(),
            BudgetLineId:    Guid.NewGuid(),
            EntryType:       EntryType.Expense,
            Amount:          100m,
            Note:            null,
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
        var result = _sut.Validate(ValidCommand() with { Amount = -50m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "AMOUNT_MUST_BE_POSITIVE");
    }

    [Fact]
    public void Amount_Positive_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { Amount = 100m });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Note_Absent_For_CreditNote_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.CreditNote, Note = null });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "NOTE_REQUIRED_FOR_ENTRY_TYPE");
    }

    [Fact]
    public void Note_Empty_For_DebitNote_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.DebitNote, Note = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "NOTE_REQUIRED_FOR_ENTRY_TYPE");
    }

    [Fact]
    public void Note_Absent_For_Expense_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.Expense, Note = null });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Note_Present_For_CreditNote_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.CreditNote, Note = "refund" });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Note_Present_For_DebitNote_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.DebitNote, Note = "adjustment" });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EntryType_Invalid_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = (EntryType)99 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateExecutionRecordCommand.EntryType));
    }

    [Fact]
    public void EntryType_Expense_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.Expense });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EntryType_CreditNote_WithNote_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.CreditNote, Note = "credit" });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void EntryType_DebitNote_WithNote_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { EntryType = EntryType.DebitNote, Note = "debit" });
        result.IsValid.ShouldBeTrue();
    }
}
