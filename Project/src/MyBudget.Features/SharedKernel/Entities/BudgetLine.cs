namespace MyBudget.Features.SharedKernel.Entities;

public sealed class BudgetLine : BaseEntity
{
    public Guid PeriodId { get; private set; }
    public Guid CategoryGroupId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public LineType LineType { get; private set; }
    public bool IsRecurring { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation
    public Period? Period { get; private set; }
    public CategoryGroup? CategoryGroup { get; private set; }
    public Category? Category { get; private set; }
    public ICollection<BudgetLineRevision> Revisions { get; private set; } = new List<BudgetLineRevision>();

    private BudgetLine() { }

    public static BudgetLine Create(
        Guid periodId,
        Guid categoryGroupId,
        Guid? categoryId,
        string name,
        LineType lineType,
        bool isRecurring,
        int displayOrder = 0)
    {
        return new BudgetLine
        {
            PeriodId        = periodId,
            CategoryGroupId = categoryGroupId,
            CategoryId      = categoryId,
            Name            = name.Trim(),
            LineType        = lineType,
            IsRecurring     = isRecurring,
            DisplayOrder    = displayOrder,
        };
    }

    public void SetDisplayOrder(int order) => DisplayOrder = order;

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;

    public void Restore()
    {
        DeletedAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(Guid categoryGroupId, Guid? categoryId, string name, LineType lineType, bool isRecurring)
    {
        CategoryGroupId = categoryGroupId;
        CategoryId      = categoryId;
        Name            = name.Trim();
        LineType        = lineType;
        IsRecurring     = isRecurring;
        UpdatedAt       = DateTimeOffset.UtcNow;
    }
}
