using MyBudget.Features.Features.BudgetStructure.CreateCycle;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateCycle;

public sealed class CreateCycleValidatorTests
{
    private readonly CreateCycleValidator _sut = new();

    private static CreateCycleCommand ValidCommand() =>
        new(Guid.NewGuid(), "2025", new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31),
            Guid.Parse("11111111-1111-1111-1111-111111111111"), null, null);

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

    [Fact]
    public void AlternateCurrencyId_WithoutExchangeRate_Fails_WithCYC_PAIR_INCOMPLETE()
    {
        var result = _sut.Validate(ValidCommand() with
        {
            AlternateCurrencyId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ExchangeRate        = null
        });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "CYC_PAIR_INCOMPLETE");
    }

    [Fact]
    public void ExchangeRate_WithoutAlternateCurrencyId_Fails_WithCYC_PAIR_INCOMPLETE()
    {
        var result = _sut.Validate(ValidCommand() with
        {
            AlternateCurrencyId = null,
            ExchangeRate        = 7.5m
        });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "CYC_PAIR_INCOMPLETE");
    }

    [Fact]
    public void BothAlternateFields_Present_Passes()
    {
        var result = _sut.Validate(ValidCommand() with
        {
            AlternateCurrencyId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            ExchangeRate        = 7.5m
        });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void BothAlternateFields_Absent_Passes()
    {
        var result = _sut.Validate(ValidCommand() with
        {
            AlternateCurrencyId = null,
            ExchangeRate        = null
        });
        result.IsValid.ShouldBeTrue();
    }
}
