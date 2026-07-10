using MyBudget.Features.Features.BudgetStructure.CreateCycle;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateCycle;

public sealed class CreateCycleValidatorTests
{
    private readonly CreateCycleValidator _sut = new();

    private static CreateCycleCommand ValidCommand() =>
        new(Guid.NewGuid(), "2025", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31));

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
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCycleCommand.Name));
    }

    [Fact]
    public void Name_TooLong_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Name = new string('a', 201) });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCycleCommand.Name));
    }

    [Fact]
    public void Name_MaxLength_Passes()
    {
        var result = _sut.Validate(ValidCommand() with { Name = new string('a', 200) });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void StartDate_AfterEndDate_Fails()
    {
        var result = _sut.Validate(ValidCommand() with
        {
            StartDate = new DateOnly(2025, 12, 31),
            EndDate   = new DateOnly(2025, 1, 1)
        });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCycleCommand.StartDate));
    }

    [Fact]
    public void StartDate_EqualToEndDate_Fails()
    {
        var result = _sut.Validate(ValidCommand() with
        {
            StartDate = new DateOnly(2025, 6, 1),
            EndDate   = new DateOnly(2025, 6, 1)
        });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void BudgetId_Empty_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { BudgetId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateCycleCommand.BudgetId));
    }
}
