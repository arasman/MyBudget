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
            LineId:          Guid.NewGuid(),
            CategoryGroupId: Guid.NewGuid(),
            CategoryId:      null,
            Name:            "Rent",
            LineType:        LineType.Expense,
            ValidFrom:       null,
            ValidTo:         null,
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

    // REQ-BL-AMOUNT-1: amount must be > 0
    [Fact]
    public void BudgetedAmount_Zero_Rejected()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetedAmount = 0m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateBudgetLineCommand.BudgetedAmount)
                                      && e.ErrorCode == "FIELD_INVALID");
    }

    [Fact]
    public void BudgetedAmount_Positive_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetedAmount = 0.01m });
        result.IsValid.ShouldBeTrue();
    }

    // REQ-BL-03: ValidFrom must not be in the past
    [Fact]
    public void ValidFrom_Yesterday_Fails()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var result = _sut.Validate(ValidCommand() with { ValidFrom = yesterday, BudgetedAmount = 500m });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateBudgetLineCommand.ValidFrom)
                                      && e.ErrorCode == "VALIDFROM_IN_PAST");
    }

    [Fact]
    public void ValidFrom_Today_Passes()
    {
        var today  = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = _sut.Validate(ValidCommand() with { ValidFrom = today, BudgetedAmount = 500m });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidFrom_Future_Passes()
    {
        var future = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var result = _sut.Validate(ValidCommand() with { ValidFrom = future, BudgetedAmount = 500m });
        result.IsValid.ShouldBeTrue();
    }

    // REQ-BL-03: ValidTo must be >= ValidFrom when both are provided
    [Fact]
    public void ValidTo_BeforeValidFrom_Fails()
    {
        var today  = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = _sut.Validate(ValidCommand() with
        {
            ValidFrom      = today.AddDays(10),
            ValidTo        = today.AddDays(5),
            BudgetedAmount = 500m
        });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateBudgetLineCommand.ValidTo));
    }

    [Fact]
    public void ValidTo_AfterValidFrom_Passes()
    {
        var today  = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = _sut.Validate(ValidCommand() with
        {
            ValidFrom      = today,
            ValidTo        = today.AddDays(30),
            BudgetedAmount = 500m
        });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidFrom_Null_NoRevisionValidation_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { ValidFrom = null, BudgetedAmount = null });
        result.IsValid.ShouldBeTrue();
    }
}
