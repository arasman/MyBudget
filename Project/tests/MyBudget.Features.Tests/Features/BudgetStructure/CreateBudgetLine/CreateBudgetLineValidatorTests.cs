using MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateBudgetLine;

public sealed class CreateBudgetLineValidatorTests
{
    private readonly CreateBudgetLineValidator _sut = new();

    private static CreateBudgetLineCommand ValidCommand() =>
        new(
            BudgetId:        Guid.NewGuid(),
            PeriodId:        Guid.NewGuid(),
            CategoryGroupId: Guid.NewGuid(),
            CategoryId:      null,
            Name:            "Rent",
            LineType:        LineType.Expense,
            IsRecurring:     true,
            BudgetedAmount:  1500m,
            Currency:        "GTQ");

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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.Name));
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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.LineType));
    }

    [Fact]
    public void LineType_Expense_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { LineType = LineType.Expense });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void LineType_LongTermSavings_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { LineType = LineType.LongTermSavings });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void LineType_PreventiveSavings_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { LineType = LineType.PreventiveSavings });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Currency_Invalid_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Currency = "EUR" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.Currency));
    }

    [Fact]
    public void Currency_GTQ_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { Currency = "GTQ" });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Currency_USD_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { Currency = "USD" });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Currency_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Currency = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.Currency));
    }

    [Fact]
    public void BudgetedAmount_Negative_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetedAmount = -1m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.BudgetedAmount));
    }

    [Fact]
    public void BudgetedAmount_Zero_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetedAmount = 0m });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void BudgetId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.BudgetId));
    }

    [Fact]
    public void PeriodId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { PeriodId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.PeriodId));
    }

    [Fact]
    public void CategoryGroupId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CategoryGroupId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.CategoryGroupId));
    }
}
