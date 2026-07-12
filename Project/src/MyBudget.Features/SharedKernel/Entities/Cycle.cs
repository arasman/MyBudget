namespace MyBudget.Features.SharedKernel.Entities;

public sealed class Cycle : BaseEntity, IAuditableEntity
{
    public Guid      BudgetId             { get; private set; }
    public string    Name                 { get; private set; } = string.Empty;
    public DateOnly  StartDate            { get; private set; }
    public DateOnly  EndDate              { get; private set; }
    public bool      IsActive             { get; private set; }
    public DateTimeOffset? DeletedAt      { get; private set; }

    // Currency fields
    public Guid      DefaultCurrencyId    { get; private set; }
    public Guid?     AlternateCurrencyId  { get; private set; }
    public decimal?  ExchangeRate         { get; private set; }

    // Navigation
    public Budget?              Budget             { get; private set; }
    public Currency?            DefaultCurrency    { get; private set; }
    public Currency?            AlternateCurrency  { get; private set; }
    public ICollection<Period>  Periods            { get; private set; } = new List<Period>();

    private Cycle() { }

    public Guid? ResolveBudgetId() => BudgetId;

    public static Cycle Create(
        Guid      budgetId,
        string    name,
        DateOnly  startDate,
        DateOnly  endDate,
        Guid      defaultCurrencyId,
        Guid?     alternateCurrencyId = null,
        decimal?  exchangeRate        = null)
    {
        return new Cycle
        {
            BudgetId            = budgetId,
            Name                = name.Trim(),
            StartDate           = startDate,
            EndDate             = endDate,
            IsActive            = false,
            DefaultCurrencyId   = defaultCurrencyId,
            AlternateCurrencyId = alternateCurrencyId,
            ExchangeRate        = exchangeRate,
        };
    }

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;

    public void Restore()
    {
        DeletedAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Update(
        string   name,
        DateOnly startDate,
        DateOnly endDate,
        Guid     defaultCurrencyId,
        Guid?    alternateCurrencyId = null,
        decimal? exchangeRate        = null)
    {
        Name                = name.Trim();
        StartDate           = startDate;
        EndDate             = endDate;
        DefaultCurrencyId   = defaultCurrencyId;
        AlternateCurrencyId = alternateCurrencyId;
        ExchangeRate        = exchangeRate;
        UpdatedAt           = DateTimeOffset.UtcNow;
    }
}
