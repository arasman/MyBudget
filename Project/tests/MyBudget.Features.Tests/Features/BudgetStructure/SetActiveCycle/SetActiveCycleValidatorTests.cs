using MyBudget.Features.Features.BudgetStructure.SetActiveCycle;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.SetActiveCycle;

public sealed class SetActiveCycleValidatorTests
{
    private readonly SetActiveCycleValidator _sut = new();

    private static SetActiveCycleCommand ValidCommand() =>
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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SetActiveCycleCommand.BudgetId));
    }

    [Fact]
    public void CycleId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CycleId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SetActiveCycleCommand.CycleId));
    }
}
