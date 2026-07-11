namespace MyBudget.Features.SharedKernel.Entities;

public sealed class CategoryGroup : BaseEntity, IAuditableEntity
{
    public Guid BudgetId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation
    public Budget? Budget { get; private set; }
    public ICollection<Category> Categories { get; private set; } = new List<Category>();

    private CategoryGroup() { }

    public Guid? ResolveBudgetId() => BudgetId;

    public static CategoryGroup Create(Guid budgetId, string name, int displayOrder)
    {
        return new CategoryGroup
        {
            BudgetId     = budgetId,
            Name         = name.Trim(),
            DisplayOrder = displayOrder,
        };
    }

    public void SetDisplayOrder(int order)
    {
        DisplayOrder = order;
        UpdatedAt    = DateTimeOffset.UtcNow;
    }

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;

    public void Restore()
    {
        DeletedAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(string name, int displayOrder)
    {
        Name         = name.Trim();
        DisplayOrder = displayOrder;
        UpdatedAt    = DateTimeOffset.UtcNow;
    }
}
