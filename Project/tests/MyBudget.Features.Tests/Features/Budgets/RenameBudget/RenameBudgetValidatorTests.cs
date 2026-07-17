using MyBudget.Features.Features.Budgets.RenameBudget;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Budgets.RenameBudget;

public sealed class RenameBudgetValidatorTests
{
    private readonly RenameBudgetValidator _sut = new();

    private static RenameBudgetCommand ValidCommand() =>
        new(Guid.NewGuid(), "New Name", Guid.NewGuid());

    [Fact]
    public void ValidName_Passes()
    {
        _sut.Validate(ValidCommand()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void NewName_Empty_ReturnsRequired()
    {
        var result = _sut.Validate(ValidCommand() with { NewName = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "BUDGET_NAME_REQUIRED");
    }

    [Fact]
    public void NewName_Whitespace_ReturnsRequired()
    {
        var result = _sut.Validate(ValidCommand() with { NewName = "   " });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "BUDGET_NAME_REQUIRED");
    }

    [Fact]
    public void NewName_TooLong_ReturnsTooLong()
    {
        var result = _sut.Validate(ValidCommand() with { NewName = new string('a', 201) });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "BUDGET_NAME_TOO_LONG");
    }

    [Fact]
    public void NewName_Exactly200Chars_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { NewName = new string('a', 200) });
        result.IsValid.ShouldBeTrue();
    }
}
