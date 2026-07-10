using MyBudget.Features.Features.BudgetStructure.DeleteCycle;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.DeleteCycle;

public sealed class DeleteCycleValidatorTests
{
    private readonly DeleteCycleValidator _sut = new();

    private static DeleteCycleCommand ValidCommand() =>
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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteCycleCommand.BudgetId));
    }

    [Fact]
    public void CycleId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CycleId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteCycleCommand.CycleId));
    }
}
