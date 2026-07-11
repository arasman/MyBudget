namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Immutable append-only revision record for a BudgetLine.
/// Never soft-deleted — cascades with BudgetLine physical delete.
/// </summary>
public sealed class BudgetLineRevision : BaseEntity, IAuditableEntity
{
    public Guid BudgetId { get; private set; }
    public Guid BudgetLineId { get; private set; }
    public decimal BudgetedAmount { get; private set; }
    public Guid CurrencyId { get; private set; }
    public DateTimeOffset RevisedAt { get; private set; }
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
        string? note = null)
    {
        return new BudgetLineRevision
        {
            BudgetId       = budgetId,
            BudgetLineId   = budgetLineId,
            BudgetedAmount = budgetedAmount,
            CurrencyId     = currencyId,
            RevisedAt      = DateTimeOffset.UtcNow,
            Note           = note,
        };
    }
}
