namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Immutable append-only revision record for a BudgetLine.
/// Never soft-deleted — cascades with BudgetLine physical delete.
/// </summary>
public sealed class BudgetLineRevision : BaseEntity
{
    public Guid BudgetLineId { get; private set; }
    public decimal BudgetedAmount { get; private set; }
    public string Currency { get; private set; } = string.Empty;
    public DateTimeOffset RevisedAt { get; private set; }
    public string? Note { get; private set; }

    // Navigation
    public BudgetLine? BudgetLine { get; private set; }

    private BudgetLineRevision() { }

    public static BudgetLineRevision Create(
        Guid budgetLineId,
        decimal budgetedAmount,
        string currency,
        string? note = null)
    {
        return new BudgetLineRevision
        {
            BudgetLineId  = budgetLineId,
            BudgetedAmount = budgetedAmount,
            Currency      = currency.ToUpperInvariant(),
            RevisedAt     = DateTimeOffset.UtcNow,
            Note          = note,
        };
    }
}
