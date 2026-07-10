namespace MyBudget.Features.SharedKernel.Entities;

public sealed class Period : BaseEntity
{
    public Guid CycleId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int PeriodNumber { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly EndDate { get; private set; }
    public bool IsClosed { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation
    public Cycle? Cycle { get; private set; }
    public ICollection<BudgetLine> BudgetLines { get; private set; } = new List<BudgetLine>();

    private Period() { }

    public static Period Create(Guid cycleId, string name, int periodNumber, DateOnly startDate, DateOnly endDate)
    {
        return new Period
        {
            CycleId      = cycleId,
            Name         = name.Trim(),
            PeriodNumber = periodNumber,
            StartDate    = startDate,
            EndDate      = endDate,
            IsClosed     = false,
        };
    }

    public void SetClosed(bool value)
    {
        IsClosed  = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;

    public void Update(string name, int periodNumber, DateOnly startDate, DateOnly endDate)
    {
        Name         = name.Trim();
        PeriodNumber = periodNumber;
        StartDate    = startDate;
        EndDate      = endDate;
        UpdatedAt    = DateTimeOffset.UtcNow;
    }
}
