using MyBudget.Features.Features.BudgetStructure.DeleteCategoryGroup;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.DeleteCategoryGroup;

public sealed class DeleteCategoryGroupValidatorTests
{
    private readonly DeleteCategoryGroupValidator _sut = new();

    private static DeleteCategoryGroupCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid());

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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteCategoryGroupCommand.BudgetId));
    }

    [Fact]
    public void GroupId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { GroupId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteCategoryGroupCommand.GroupId));
    }
}
