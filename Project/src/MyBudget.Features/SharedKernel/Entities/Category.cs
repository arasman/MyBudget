namespace MyBudget.Features.SharedKernel.Entities;

public sealed class Category : BaseEntity
{
    public Guid CategoryGroupId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation
    public CategoryGroup? CategoryGroup { get; private set; }

    private Category() { }

    public static Category Create(Guid categoryGroupId, string name, int displayOrder)
    {
        return new Category
        {
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

    public void Update(string name, int displayOrder)
    {
        Name         = name.Trim();
        DisplayOrder = displayOrder;
        UpdatedAt    = DateTimeOffset.UtcNow;
    }
}
