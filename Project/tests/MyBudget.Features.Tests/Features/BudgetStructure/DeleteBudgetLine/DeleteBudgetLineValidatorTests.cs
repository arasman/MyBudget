using MyBudget.Features.Features.BudgetStructure.DeleteBudgetLine;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.DeleteBudgetLine;

public sealed class DeleteBudgetLineValidatorTests
{
    private readonly DeleteBudgetLineValidator _sut = new();

    private static DeleteBudgetLineCommand ValidCommand() =>
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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteBudgetLineCommand.BudgetId));
    }

    [Fact]
    public void PeriodId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { PeriodId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteBudgetLineCommand.PeriodId));
    }

    [Fact]
    public void LineId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { LineId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(DeleteBudgetLineCommand.LineId));
    }
}
