namespace MyBudget.Features.SharedKernel.Entities;

public sealed class BudgetLine : BaseEntity, IAuditableEntity
{
    public Guid BudgetId { get; private set; }
    public Guid CategoryGroupId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public LineType LineType { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public int DisplayOrder { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation
    public CategoryGroup? CategoryGroup { get; private set; }
    public Category? Category { get; private set; }
    public ICollection<BudgetLineRevision> Revisions { get; private set; } = new List<BudgetLineRevision>();

    private BudgetLine() { }

    public Guid? ResolveBudgetId() => BudgetId;

    public static BudgetLine Create(
        Guid budgetId,
        Guid categoryGroupId,
        Guid? categoryId,
        string name,
        LineType lineType,
        DateOnly startDate,
        DateOnly? endDate,
        decimal initialAmount,
        Guid currencyId,
        int displayOrder = 0)
    {
        var line = new BudgetLine
        {
            BudgetId        = budgetId,
            CategoryGroupId = categoryGroupId,
            CategoryId      = categoryId,
            Name            = name.Trim(),
            LineType        = lineType,
            StartDate       = startDate,
            EndDate         = endDate,
            DisplayOrder    = displayOrder,
        };

        line.AddInitialRevision(startDate, endDate, initialAmount, currencyId);

        return line;
    }

    public void SetDisplayOrder(int order) => DisplayOrder = order;

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;

    public void Restore()
    {
        DeletedAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(Guid categoryGroupId, Guid? categoryId, string name, LineType lineType)
    {
        CategoryGroupId = categoryGroupId;
        CategoryId      = categoryId;
        Name            = name.Trim();
        LineType        = lineType;
        UpdatedAt       = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Splits the enclosing revision at <paramref name="newValidFrom"/>, inserting a new revision
    /// with the given amount. Maintains a gapless revision chain.
    /// Edge Case B: if newValidFrom == enclosing.ValidFrom, overwrites in-place (no split).
    /// </summary>
    public void SplitRevision(DateOnly newValidFrom, DateOnly? newValidTo, decimal amount, Guid currencyId)
    {
        var enclosing = Revisions.FirstOrDefault(r =>
            r.ValidFrom <= newValidFrom && (r.ValidTo is null || r.ValidTo >= newValidFrom))
            ?? throw new InvalidOperationException(
                $"No enclosing revision found for ValidFrom={newValidFrom}.");

        // Edge Case B: exact boundary match — overwrite in-place
        if (newValidFrom == enclosing.ValidFrom)
        {
            enclosing.SetAmount(amount, currencyId);
            enclosing.SetValidTo(newValidTo);
            return;
        }

        var enclosingOriginalValidTo = enclosing.ValidTo;

        // Trim the enclosing revision
        enclosing.SetValidTo(newValidFrom.AddDays(-1));

        // Insert new revision
        Revisions.Add(BudgetLineRevision.Create(
            BudgetId, Id, amount, currencyId, newValidFrom, newValidTo));

        // Insert tail if the new revision closes before the enclosing upper bound
        if (newValidTo.HasValue &&
            (enclosingOriginalValidTo is null || enclosingOriginalValidTo > newValidTo))
        {
            Revisions.Add(BudgetLineRevision.Create(
                BudgetId, Id,
                enclosing.BudgetedAmount, enclosing.CurrencyId,
                newValidTo.Value.AddDays(1), enclosingOriginalValidTo));
        }
    }

    private void AddInitialRevision(DateOnly validFrom, DateOnly? validTo, decimal amount, Guid currencyId)
    {
        Revisions.Add(BudgetLineRevision.Create(BudgetId, Id, amount, currencyId, validFrom, validTo));
    }
}
