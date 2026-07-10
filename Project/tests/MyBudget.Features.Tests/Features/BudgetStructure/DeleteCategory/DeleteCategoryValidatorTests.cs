using MyBudget.Features.Features.BudgetStructure.DeleteCategory;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.DeleteCategory;

public sealed class DeleteCategoryValidatorTests
{
    private readonly DeleteCategoryValidator _sut = new();

    private static DeleteCategoryCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteCategoryCommand.BudgetId));
    }

    [Fact]
    public void CategoryGroupId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CategoryGroupId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteCategoryCommand.CategoryGroupId));
    }

    [Fact]
    public void CategoryId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CategoryId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteCategoryCommand.CategoryId));
    }
}
