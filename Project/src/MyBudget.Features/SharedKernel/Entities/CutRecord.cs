namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Periodic financial snapshot header for a budget.
/// UNIQUE(BudgetId, CutDate) enforced at DB level.
/// ProjectionsJson is a nullable placeholder — no schema defined yet.
/// </summary>
public sealed class CutRecord : BaseEntity, IAuditableEntity
{
    public Guid      BudgetId        { get; private set; }
    public DateOnly  CutDate         { get; private set; }
    public decimal   ExchangeRate    { get; private set; }
    public string?   ProjectionsJson { get; private set; }

    // Navigation
    public ICollection<CutBankAccount> CutBankAccounts { get; private set; } = new List<CutBankAccount>();

    private CutRecord() { }

    public Guid? ResolveBudgetId() => BudgetId;

    public static CutRecord Create(
        Guid     budgetId,
        DateOnly cutDate,
        decimal  exchangeRate,
        string?  projectionsJson = null)
    {
        return new CutRecord
        {
            BudgetId        = budgetId,
            CutDate         = cutDate,
            ExchangeRate    = exchangeRate,
            ProjectionsJson = projectionsJson,
        };
    }

    public void Update(decimal exchangeRate, string? projectionsJson = null)
    {
        ExchangeRate    = exchangeRate;
        ProjectionsJson = projectionsJson;
        UpdatedAt       = DateTimeOffset.UtcNow;
    }
}
