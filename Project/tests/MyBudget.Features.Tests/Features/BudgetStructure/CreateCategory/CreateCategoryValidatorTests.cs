using MyBudget.Features.Features.BudgetStructure.CreateCategory;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateCategory;

public sealed class CreateCategoryValidatorTests
{
    private readonly CreateCategoryValidator _sut = new();

    private static CreateCategoryCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Rent", 1);

    [Fact]
    public void ValidPayload_Passes()
    {
        var result = _sut.Validate(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void BudgetId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCategoryCommand.BudgetId));
    }

    [Fact]
    public void CategoryGroupId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CategoryGroupId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCategoryCommand.CategoryGroupId));
    }

    [Fact]
    public void Name_Missing_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Name = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCategoryCommand.Name));
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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCategoryCommand.DisplayOrder));
    }

    [Fact]
    public void DisplayOrder_Negative_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { DisplayOrder = -1 });
        result.IsValid.ShouldBeFalse();
    }
}
