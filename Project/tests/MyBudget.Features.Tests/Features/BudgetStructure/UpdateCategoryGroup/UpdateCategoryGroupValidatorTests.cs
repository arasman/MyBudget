using MyBudget.Features.Features.BudgetStructure.UpdateCategoryGroup;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateCategoryGroup;

public sealed class UpdateCategoryGroupValidatorTests
{
    private readonly UpdateCategoryGroupValidator _sut = new();

    private static UpdateCategoryGroupCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Housing", 1);

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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateCategoryGroupCommand.Name));
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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateCategoryGroupCommand.DisplayOrder));
    }

    [Fact]
    public void BudgetId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void GroupId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { GroupId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateCategoryGroupCommand.GroupId));
    }
}
