using MyBudget.Features.Features.BudgetStructure.CreateBudgetLineRevision;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BudgetStructure.CreateBudgetLineRevision;

/// <summary>
/// T2.9 — Validator unit tests for CreateBudgetLineRevisionCommand.
/// REQ-BLR-02: ValidFrom must be today or future; Amount must be > 0.
/// Note: date-range bounds guard (line.StartDate..EndDate) is a handler concern — not validated here.
/// </summary>
public sealed class CreateBudgetLineRevisionValidatorTests
{
    private readonly CreateBudgetLineRevisionValidator _sut = new();

    private static CreateBudgetLineRevisionCommand ValidCommand() =>
        new(
            BudgetId:  Guid.NewGuid(),
            LineId:    Guid.NewGuid(),
            ValidFrom: DateOnly.FromDateTime(DateTime.UtcNow),
            ValidTo:   null,
            Amount:    1000m,
            CurrencyId: null);

    [Fact]
    public void ValidPayload_Passes()
    {
        var result = _sut.Validate(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    // ── ValidFrom rules ──────────────────────────────────────────────────────

    [Fact]
    public void ValidFrom_Yesterday_Fails_WithCode_REVISION_VALID_FROM_IN_PAST()
    {
        var cmd    = ValidCommand() with { ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1) };
        var result = _sut.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(CreateBudgetLineRevisionCommand.ValidFrom)
            && e.ErrorCode  == "REVISION_VALID_FROM_IN_PAST");
    }

    [Fact]
    public void ValidFrom_Today_Passes()
    {
        var cmd    = ValidCommand() with { ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow) };
        var result = _sut.Validate(cmd);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidFrom_Tomorrow_Passes()
    {
        var cmd    = ValidCommand() with { ValidFrom = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1) };
        var result = _sut.Validate(cmd);
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ValidFrom_Default_Fails()
    {
        var cmd    = ValidCommand() with { ValidFrom = default };
        var result = _sut.Validate(cmd);
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineRevisionCommand.ValidFrom));
    }

    // ── Amount rules ─────────────────────────────────────────────────────────

    [Fact]
    public void Amount_Zero_Fails()
    {
        var cmd    = ValidCommand() with { Amount = 0m };
        var result = _sut.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e =>
            e.PropertyName == nameof(CreateBudgetLineRevisionCommand.Amount)
            && e.ErrorCode  == "FIELD_INVALID");
    }

    [Fact]
    public void Amount_Negative_Fails()
    {
        var cmd    = ValidCommand() with { Amount = -500m };
        var result = _sut.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineRevisionCommand.Amount));
    }

    [Fact]
    public void Amount_Positive_Passes()
    {
        var cmd    = ValidCommand() with { Amount = 0.01m };
        var result = _sut.Validate(cmd);
        result.IsValid.ShouldBeTrue();
    }

    // ── Required fields ──────────────────────────────────────────────────────

    [Fact]
    public void BudgetId_Empty_Fails()
    {
        var cmd    = ValidCommand() with { BudgetId = Guid.Empty };
        var result = _sut.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineRevisionCommand.BudgetId));
    }

    [Fact]
    public void LineId_Empty_Fails()
    {
        var cmd    = ValidCommand() with { LineId = Guid.Empty };
        var result = _sut.Validate(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBudgetLineRevisionCommand.LineId));
    }
}
