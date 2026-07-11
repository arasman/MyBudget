namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Immutable append-only revision record for a BudgetLine.
/// Never soft-deleted — cascades with BudgetLine physical delete.
/// </summary>
public sealed class BudgetLineRevision : BaseEntity, IAuditableEntity
{
    public Guid BudgetLineId { get; private set; }
    public decimal BudgetedAmount { get; private set; }
    public Guid CurrencyId { get; private set; }
    public DateTimeOffset RevisedAt { get; private set; }
    public string? Note { get; private set; }

    // Navigation
    public BudgetLine? BudgetLine { get; private set; }
    public Currency? Currency { get; private set; }

    private BudgetLineRevision() { }

    /// <summary>
    /// BudgetId is not directly available on BudgetLineRevision (Revision → BudgetLine → Period → Cycle → Budget).
    /// Returns null; BudgetId is resolved via Dapper fallback at audit time.
    /// </summary>
    public Guid? ResolveBudgetId() => null;

    public static BudgetLineRevision Create(
        Guid budgetLineId,
        decimal budgetedAmount,
        Guid currencyId,
        string? note = null)
    {
        return new BudgetLineRevision
        {
            BudgetLineId   = budgetLineId,
            BudgetedAmount = budgetedAmount,
            CurrencyId     = currencyId,
            RevisedAt      = DateTimeOffset.UtcNow,
            Note           = note,
        };
    }
}
