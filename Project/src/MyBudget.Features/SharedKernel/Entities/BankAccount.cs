namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Budget-scoped bank account catalog entry with soft-delete semantics.
/// CurrencyId is immutable after creation (enforced at handler level).
/// </summary>
public sealed class BankAccount : BaseEntity, IAuditableEntity
{
    public Guid               BudgetId     { get; private set; }
    public Guid               CurrencyId   { get; private set; }
    public string             Alias        { get; private set; } = string.Empty;
    public bool               IsPositive   { get; private set; }
    public int                DisplayOrder { get; private set; }
    public DateTimeOffset?    DeletedAt    { get; private set; }

    private BankAccount() { }

    public Guid? ResolveBudgetId() => BudgetId;

    public static BankAccount Create(
        Guid   budgetId,
        Guid   currencyId,
        string alias,
        bool   isPositive,
        int    displayOrder)
    {
        return new BankAccount
        {
            BudgetId     = budgetId,
            CurrencyId   = currencyId,
            Alias        = alias.Trim(),
            IsPositive   = isPositive,
            DisplayOrder = displayOrder,
        };
    }

    public void Update(string alias, bool isPositive, int displayOrder)
    {
        Alias        = alias.Trim();
        IsPositive   = isPositive;
        DisplayOrder = displayOrder;
        UpdatedAt    = DateTimeOffset.UtcNow;
    }

    public void SoftDelete()
    {
        DeletedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
