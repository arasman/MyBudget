using MyBudget.Features.Features.BudgetStructure.SetPeriodStatus;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.SetPeriodStatus;

public sealed class SetPeriodStatusValidatorTests
{
    private readonly SetPeriodStatusValidator _sut = new();

    private static SetPeriodStatusCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true);

    [Fact]
    public void ValidPayload_Close_Passes()
    {
        var result = _sut.Validate(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidPayload_Open_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { IsClosed = false });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void BudgetId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SetPeriodStatusCommand.BudgetId));
    }

    [Fact]
    public void CycleId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CycleId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SetPeriodStatusCommand.CycleId));
    }

    [Fact]
    public void PeriodId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { PeriodId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(SetPeriodStatusCommand.PeriodId));
    }
}
