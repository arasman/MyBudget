using MyBudget.Features.Features.BudgetStructure.CreatePeriod;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreatePeriod;

public sealed class CreatePeriodValidatorTests
{
    private readonly CreatePeriodValidator _sut = new();

    private static CreatePeriodCommand ValidCommand() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "January", 1, new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 31));

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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreatePeriodCommand.Name));
    }

    [Fact]
    public void Name_TooLong_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Name = new string('a', 201) });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void PeriodNumber_Zero_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { PeriodNumber = 0 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreatePeriodCommand.PeriodNumber));
    }

    [Fact]
    public void PeriodNumber_Negative_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { PeriodNumber = -1 });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void StartDate_AfterEndDate_Fails()
    {
        var result = _sut.Validate(ValidCommand() with
        {
            StartDate = new DateOnly(2025, 1, 31),
            EndDate   = new DateOnly(2025, 1, 1)
        });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreatePeriodCommand.StartDate));
    }

    [Fact]
    public void StartDate_EqualToEndDate_Fails()
    {
        var result = _sut.Validate(ValidCommand() with
        {
            StartDate = new DateOnly(2025, 1, 15),
            EndDate   = new DateOnly(2025, 1, 15)
        });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void BudgetId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CycleId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { CycleId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreatePeriodCommand.CycleId));
    }
}
