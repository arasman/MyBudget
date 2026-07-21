using MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateBudgetLine;

// TODO PR2a: full rewrite — validator tests for new command shape (StartDate, EndDate, InitialAmount)
public sealed class CreateBudgetLineValidatorTests
{
    private readonly CreateBudgetLineValidator _sut = new();

    private static CreateBudgetLineCommand ValidCommand() =>
        new(
            BudgetId:        Guid.NewGuid(),
            CategoryGroupId: Guid.NewGuid(),
            CategoryId:      null,
            Name:            "Rent",
            LineType:        LineType.Expense,
            StartDate:       new DateOnly(2025, 1, 1),
            EndDate:         null,
            InitialAmount:   1500m,
            CurrencyId:      CurrencySeeds.GtqId);

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
    public void InitialAmount_Negative_Fails()
    {
        // TODO PR2a: rename from BudgetedAmount to InitialAmount in validator and tests
        var result = _sut.Validate(ValidCommand() with { InitialAmount = -1m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.InitialAmount));
    }

    // REQ-BL-AMOUNT-1: amount must be > 0
    [Fact]
    public void InitialAmount_Zero_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { InitialAmount = 0m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.InitialAmount)
                                      && e.ErrorCode == "FIELD_INVALID");
    }

    [Fact]
    public void InitialAmount_Positive_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { InitialAmount = 0.01m });
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
    public void CategoryGroupId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CategoryGroupId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineCommand.CategoryGroupId));
    }
}
