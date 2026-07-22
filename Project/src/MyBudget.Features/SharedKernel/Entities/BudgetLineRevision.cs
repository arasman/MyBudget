namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Append-only revision record for a BudgetLine with a date-range validity window.
/// Never soft-deleted — cascades with BudgetLine physical delete.
/// </summary>
public sealed class BudgetLineRevision : BaseEntity, IAuditableEntity
{
    public Guid BudgetId { get; private set; }
    public Guid BudgetLineId { get; private set; }
    public decimal BudgetedAmount { get; private set; }
    public Guid CurrencyId { get; private set; }
    public DateOnly ValidFrom { get; private set; }
    public DateOnly? ValidTo { get; private set; }
    public string? Note { get; private set; }

    // Navigation
    public BudgetLine? BudgetLine { get; private set; }
    public Currency? Currency { get; private set; }

    private BudgetLineRevision() { }

    public Guid? ResolveBudgetId() => BudgetId;

    public static BudgetLineRevision Create(
        Guid budgetId,
        Guid budgetLineId,
        decimal budgetedAmount,
        Guid currencyId,
        DateOnly validFrom,
        DateOnly? validTo,
        string? note = null)
    {
        return new BudgetLineRevision
        {
            BudgetId       = budgetId,
            BudgetLineId   = budgetLineId,
            BudgetedAmount = budgetedAmount,
            CurrencyId     = currencyId,
            ValidFrom      = validFrom,
            ValidTo        = validTo,
            Note           = note,
        };
    }

    /// <summary>Used by SplitRevision to trim the enclosing revision's upper bound.</summary>
    public void SetValidTo(DateOnly? validTo) => ValidTo = validTo;

    /// <summary>Used by Edge Case B: overwrite amount/currency in-place when split is at exact boundary.</summary>
    public void SetAmount(decimal amount, Guid currencyId)
    {
        BudgetedAmount = amount;
        CurrencyId     = currencyId;
    }
}
