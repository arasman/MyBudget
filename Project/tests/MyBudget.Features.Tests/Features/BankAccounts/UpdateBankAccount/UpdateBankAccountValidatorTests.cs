using MyBudget.Features.Features.BankAccounts.UpdateBankAccount;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BankAccounts.UpdateBankAccount;

/// <summary>
/// BA-3: UpdateBankAccount does NOT include CurrencyId (immutable after creation).
/// </summary>
public sealed class UpdateBankAccountValidatorTests
{
    private readonly UpdateBankAccountValidator _sut = new();

    private static UpdateBankAccountCommand ValidCommand() =>
        new(
            BudgetId:     Guid.NewGuid(),
            AccountId:    Guid.NewGuid(),
            Alias:        "Cuenta Principal",
            IsPositive:   true,
            DisplayOrder: 2);

    [Fact]
    public void Command_Has_No_CurrencyId_Property()
    {
        // CurrencyId MUST NOT exist on the update command (spec BA-3)
        var props = typeof(UpdateBankAccountCommand).GetProperties();
        props.ShouldNotContain(p => p.Name == "CurrencyId");
    }

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
        var longAlias = new string('X', 101);
        var result    = _sut.Validate(ValidCommand() with { Alias = longAlias });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "ALIAS_TOO_LONG");
    }

    [Fact]
    public void DisplayOrder_Negative_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { DisplayOrder = -1 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "DISPLAY_ORDER_INVALID");
    }

    [Fact]
    public void Valid_Payload_Passes()
    {
        var result = _sut.Validate(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }
}
