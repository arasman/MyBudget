using MyBudget.Features.Features.BudgetStructure.UpdateCategory;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateCategory;

public sealed class UpdateCategoryValidatorTests
{
    private readonly UpdateCategoryValidator _sut = new();

    private static UpdateCategoryCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Rent & Mortgage", 2);

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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateCategoryCommand.BudgetId));
    }

    [Fact]
    public void CategoryGroupId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CategoryGroupId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateCategoryCommand.CategoryGroupId));
    }

    [Fact]
    public void CategoryId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CategoryId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateCategoryCommand.CategoryId));
    }

    [Fact]
    public void Name_Missing_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Name = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateCategoryCommand.Name));
    }

    [Fact]
    public void Name_TooLong_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Name = new string('a', 201) });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void DisplayOrder_Zero_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { DisplayOrder = 0 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateCategoryCommand.DisplayOrder));
    }
}
