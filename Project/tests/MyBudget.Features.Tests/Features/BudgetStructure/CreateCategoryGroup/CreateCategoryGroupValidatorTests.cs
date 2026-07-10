using MyBudget.Features.Features.BudgetStructure.CreateCategoryGroup;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateCategoryGroup;

public sealed class CreateCategoryGroupValidatorTests
{
    private readonly CreateCategoryGroupValidator _sut = new();

    private static CreateCategoryGroupCommand ValidCommand() =>
        new(Guid.NewGuid(), "Housing", 1);

    [Fact]
    public void ValidPayload_Passes()
    {
        var result = _sut.Validate(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Name_Missing_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Name = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCategoryGroupCommand.Name));
    }

    [Fact]
    public void Name_TooLong_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Name = new string('a', 201) });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Name_MaxLength_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { Name = new string('a', 200) });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void DisplayOrder_Zero_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { DisplayOrder = 0 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCategoryGroupCommand.DisplayOrder));
    }

    [Fact]
    public void DisplayOrder_Negative_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { DisplayOrder = -1 });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void BudgetId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCategoryGroupCommand.BudgetId));
    }
}
