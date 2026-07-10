using MyBudget.Features.Features.BudgetStructure.ReorderCategories;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.ReorderCategories;

public sealed class ReorderCategoriesValidatorTests
{
    private readonly ReorderCategoriesValidator _sut = new();

    private static ReorderCategoriesCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()]);

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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReorderCategoriesCommand.BudgetId));
    }

    [Fact]
    public void CategoryGroupId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CategoryGroupId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReorderCategoriesCommand.CategoryGroupId));
    }

    [Fact]
    public void OrderedIds_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { OrderedIds = [] });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReorderCategoriesCommand.OrderedIds));
    }

    [Fact]
    public void OrderedIds_WithOneItem_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { OrderedIds = [Guid.NewGuid()] });
        result.IsValid.ShouldBeTrue();
    }
}
