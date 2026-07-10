namespace MyBudget.Features.SharedKernel.Entities;

public sealed class Cycle : BaseEntity
{
    public Guid BudgetId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation
    public Budget? Budget { get; private set; }
    public ICollection<Period> Periods { get; private set; } = new List<Period>();

    private Cycle() { }

    public static Cycle Create(Guid budgetId, string name, DateOnly startDate, DateOnly endDate)
    {
        return new Cycle
        {
            BudgetId  = budgetId,
            Name      = name.Trim(),
            StartDate = startDate,
            EndDate   = endDate,
            IsActive  = false,
        };
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;

    public void Update(string name, DateOnly startDate, DateOnly endDate)
    {
        Name      = name.Trim();
        StartDate = startDate;
        EndDate   = endDate;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
