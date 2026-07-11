using MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.UpdateBudgetLine;

public sealed class UpdateBudgetLineValidatorTests
{
    private readonly UpdateBudgetLineValidator _sut = new();

    private static UpdateBudgetLineCommand ValidCommand() =>
        new(
            BudgetId:        Guid.NewGuid(),
            PeriodId:        Guid.NewGuid(),
            LineId:          Guid.NewGuid(),
            CategoryGroupId: Guid.NewGuid(),
            CategoryId:      null,
            Name:            "Rent",
            LineType:        LineType.Expense,
            IsRecurring:     true,
            BudgetedAmount:  2000m,
            CurrencyId:      CurrencySeeds.UsdId);

    [Fact]
    public void ValidPayload_Passes()
    {
        var result = _sut.Validate(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidPayload_NoCurrencyId_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { CurrencyId = null });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void BudgetId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateBudgetLineCommand.BudgetId));
    }

    [Fact]
    public void PeriodId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { PeriodId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateBudgetLineCommand.PeriodId));
    }

    [Fact]
    public void LineId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { LineId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateBudgetLineCommand.LineId));
    }

    [Fact]
    public void CategoryGroupId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CategoryGroupId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateBudgetLineCommand.CategoryGroupId));
    }

    [Fact]
    public void Name_Missing_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Name = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateBudgetLineCommand.Name));
    }

    [Fact]
    public void Name_TooLong_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Name = new string('a', 201) });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void LineType_Invalid_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { LineType = (LineType)99 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateBudgetLineCommand.LineType));
    }

    [Fact]
    public void BudgetedAmount_Negative_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetedAmount = -0.01m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateBudgetLineCommand.BudgetedAmount));
    }
}
