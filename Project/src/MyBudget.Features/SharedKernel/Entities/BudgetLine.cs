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

    /// <summary>
    /// Deletes the revision identified by <paramref name="revisionId"/> and repairs the chain
    /// to remain gapless.
    /// <list type="bullet">
    ///   <item>Original revision (earliest ValidFrom) — throws CANNOT_DELETE_ORIGINAL_REVISION.</item>
    ///   <item>Active executions in the range — throws REVISION_HAS_ACTIVE_EXECUTIONS (caller resolves).</item>
    ///   <item>Middle revision — predecessor's ValidTo is extended to the deleted revision's ValidTo.</item>
    ///   <item>Last revision (no successor) — predecessor becomes open-ended (ValidTo = null).</item>
    /// </list>
    /// The handler MUST also call <c>_db.BudgetLineRevisions.Remove(target)</c> after this method.
    /// </summary>
    /// <param name="revisionId">ID of the revision to delete.</param>
    /// <param name="hasActiveExecutions">
    ///   Pass <c>true</c> if the caller found non-soft-deleted execution records whose
    ///   OperationDate falls within this revision's validity window.
    /// </param>
    /// <returns>The removed <see cref="BudgetLineRevision"/> for the handler to physically delete.</returns>
    public BudgetLineRevision DeleteRevision(Guid revisionId, bool hasActiveExecutions)
    {
        var target = Revisions.FirstOrDefault(r => r.Id == revisionId)
            ?? throw new InvalidOperationException(
                $"Revision {revisionId} not found on BudgetLine {Id}.");

        if (hasActiveExecutions)
            throw new InvalidOperationException("REVISION_HAS_ACTIVE_EXECUTIONS");

        // The original revision is the one with the smallest ValidFrom in the chain.
        var original = Revisions.OrderBy(r => r.ValidFrom).First();
        if (target.Id == original.Id)
            throw new InvalidOperationException("CANNOT_DELETE_ORIGINAL_REVISION");

        // Locate predecessor (the revision whose ValidFrom is immediately before target's)
        var sorted      = Revisions.OrderBy(r => r.ValidFrom).ToList();
        var targetIndex = sorted.FindIndex(r => r.Id == target.Id);
        var predecessor = sorted[targetIndex - 1];

        // Determine the successor (if any)
        var hasSuccessor = targetIndex < sorted.Count - 1;

        if (hasSuccessor)
        {
            // Middle revision: predecessor absorbs the deleted revision's range.
            // The successor [target.ValidTo + 1 day, ...] stays unchanged.
            // Gapless: predecessor.ValidTo = target.ValidTo (covers up to where target ended).
            predecessor.SetValidTo(target.ValidTo);
        }
        else
        {
            // Last revision: predecessor becomes open-ended.
            predecessor.SetValidTo(null);
        }

        Revisions.Remove(target);
        return target;
    }

    /// <summary>
    /// Updates the date range of the BudgetLine. Guards that no existing revision would be
    /// orphaned outside the new range.
    /// Throws RANGE_WOULD_ORPHAN_REVISION if any revision falls outside [startDate, endDate].
    /// </summary>
    public void UpdateDateRange(DateOnly startDate, DateOnly? endDate)
    {
        foreach (var revision in Revisions)
        {
            // A revision is orphaned when its ValidFrom < new startDate
            // or its ValidTo (or null = open-ended) is beyond the new endDate.
            if (revision.ValidFrom < startDate)
                throw new InvalidOperationException("RANGE_WOULD_ORPHAN_REVISION");

            if (endDate.HasValue)
            {
                // Revision extends beyond the new end date
                if (revision.ValidTo is null || revision.ValidTo > endDate)
                    throw new InvalidOperationException("RANGE_WOULD_ORPHAN_REVISION");
            }
        }

        StartDate = startDate;
        EndDate   = endDate;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void AddInitialRevision(DateOnly validFrom, DateOnly? validTo, decimal amount, Guid currencyId)
    {
        Revisions.Add(BudgetLineRevision.Create(BudgetId, Id, amount, currencyId, validFrom, validTo));
    }
}
