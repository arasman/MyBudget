using MyBudget.Features.Features.BudgetStructure.ReorderCategoryGroups;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.ReorderCategoryGroups;

public sealed class ReorderCategoryGroupsValidatorTests
{
    private readonly ReorderCategoryGroupsValidator _sut = new();

    private static ReorderCategoryGroupsCommand ValidCommand() =>
        new(Guid.NewGuid(), [Guid.NewGuid(), Guid.NewGuid()]);

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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReorderCategoryGroupsCommand.BudgetId));
    }

    [Fact]
    public void OrderedIds_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { OrderedIds = [] });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(ReorderCategoryGroupsCommand.OrderedIds));
    }

    [Fact]
    public void OrderedIds_WithOneItem_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { OrderedIds = [Guid.NewGuid()] });
        result.IsValid.ShouldBeTrue();
    }
}
