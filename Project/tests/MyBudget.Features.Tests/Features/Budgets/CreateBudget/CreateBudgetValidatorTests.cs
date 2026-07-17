using MyBudget.Features.Features.Budgets.CreateBudget;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Budgets.CreateBudget;

public sealed class CreateBudgetValidatorTests
{
    private readonly CreateBudgetValidator _sut = new();

    private static CreateBudgetCommand ValidCommand() =>
        new("Household", Guid.NewGuid());

    [Fact]
    public void ValidName_Passes()
    {
        _sut.Validate(ValidCommand()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Name_Empty_ReturnsRequired()
    {
        var result = _sut.Validate(ValidCommand() with { Name = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "BUDGET_NAME_REQUIRED");
    }

    [Fact]
    public void Name_Whitespace_ReturnsRequired()
    {
        var result = _sut.Validate(ValidCommand() with { Name = "   " });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "BUDGET_NAME_REQUIRED");
    }

    [Fact]
    public void Name_TooLong_ReturnsTooLong()
    {
        var result = _sut.Validate(ValidCommand() with { Name = new string('a', 201) });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "BUDGET_NAME_TOO_LONG");
    }

    [Fact]
    public void Name_Exactly200Chars_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { Name = new string('a', 200) });
        result.IsValid.ShouldBeTrue();
    }
}
