using MyBudget.Features.Features.CurrentSituation.UpsertCutRecord;
using Shouldly;

namespace MyBudget.Features.Tests.Features.CurrentSituation;

public sealed class UpsertCutRecordValidatorTests
{
    private readonly UpsertCutRecordValidator _sut = new();

    private static UpsertCutRecordCommand ValidCommand() =>
        new(
            BudgetId:        Guid.NewGuid(),
            CutDate:         new DateOnly(2026, 7, 28),
            ExchangeRate:    7.8m,
            ProjectionsJson: null,
            Accounts:        new List<UpsertCutBankAccountItem>
            {
                new(Guid.NewGuid(), 1000m),
            });

    [Fact]
    public void ExchangeRate_Zero_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { ExchangeRate = 0m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "EXCHANGE_RATE_MUST_BE_POSITIVE");
    }

    [Fact]
    public void ExchangeRate_Negative_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { ExchangeRate = -1m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "EXCHANGE_RATE_MUST_BE_POSITIVE");
    }

    [Fact]
    public void ExchangeRate_Positive_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { ExchangeRate = 0.01m });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Account_Balance_Negative_Rejected()
    {
        var accounts = new List<UpsertCutBankAccountItem> { new(Guid.NewGuid(), -1m) };
        var result   = _sut.Validate(ValidCommand() with { Accounts = accounts });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "BALANCE_MUST_BE_NON_NEGATIVE");
    }

    [Fact]
    public void Account_BankAccountId_Empty_Rejected()
    {
        var accounts = new List<UpsertCutBankAccountItem> { new(Guid.Empty, 100m) };
        var result   = _sut.Validate(ValidCommand() with { Accounts = accounts });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "FIELD_REQUIRED");
    }

    [Fact]
    public void Valid_Payload_Passes()
    {
        var result = _sut.Validate(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }
}
