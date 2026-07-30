using MyBudget.Features.Features.BankAccounts.CreateBankAccount;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BankAccounts.CreateBankAccount;

public sealed class CreateBankAccountValidatorTests
{
    private readonly CreateBankAccountValidator _sut = new();

    private static CreateBankAccountCommand ValidCommand() =>
        new(
            BudgetId:     Guid.NewGuid(),
            CurrencyId:   CurrencySeeds.GtqId,
            Alias:        "Caja GTQ",
            IsPositive:   true,
            DisplayOrder: 1);

    [Fact]
    public void Alias_Empty_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { Alias = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "FIELD_REQUIRED");
    }

    [Fact]
    public void Alias_101_Chars_Rejected()
    {
        var longAlias = new string('A', 101);
        var result    = _sut.Validate(ValidCommand() with { Alias = longAlias });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "ALIAS_TOO_LONG");
    }

    [Fact]
    public void Alias_100_Chars_Passes()
    {
        var alias  = new string('A', 100);
        var result = _sut.Validate(ValidCommand() with { Alias = alias });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void DisplayOrder_Negative_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { DisplayOrder = -1 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "DISPLAY_ORDER_INVALID");
    }

    [Fact]
    public void DisplayOrder_Zero_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { DisplayOrder = 0 });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CurrencyId_Empty_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { CurrencyId = Guid.Empty });
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
