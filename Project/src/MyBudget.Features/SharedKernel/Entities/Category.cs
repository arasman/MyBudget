namespace MyBudget.Features.SharedKernel.Entities;

public sealed class Category : BaseEntity, IAuditableEntity
{
    public Guid BudgetId { get; private set; }
    public Guid CategoryGroupId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation
    public CategoryGroup? CategoryGroup { get; private set; }

    private Category() { }

    public Guid? ResolveBudgetId() => BudgetId;

    public static Category Create(Guid budgetId, Guid categoryGroupId, string name, int displayOrder)
    {
        return new Category
        {
            BudgetId        = budgetId,
            CategoryGroupId = categoryGroupId,
            Name            = name.Trim(),
            DisplayOrder    = displayOrder,
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
