namespace MyBudget.Features.SharedKernel.Entities;

public sealed class ExecutionRecord : BaseEntity, IAuditableEntity
{
    public Guid      BudgetId        { get; private set; }
    public Guid      PeriodId        { get; private set; }
    public Guid      BudgetLineId    { get; private set; }
    public EntryType EntryType       { get; private set; }
    public decimal   Amount          { get; private set; }
    public string?   Note            { get; private set; }
    public Guid      CurrencyId      { get; private set; }
    public decimal?  ExchangeRate    { get; private set; }
    public decimal?  ExchangeRateTo  { get; private set; }
    public Guid?     AccountId       { get; private set; }
    public Guid?     PaymentMethodId { get; private set; }
    public DateOnly?  OperationDate   { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }

    // Navigation
    public BudgetLine? BudgetLine { get; private set; }

    private ExecutionRecord() { }

    public Guid? ResolveBudgetId() => BudgetId;

    public static ExecutionRecord Create(
        Guid      budgetId,
        Guid      periodId,
        Guid      budgetLineId,
        EntryType entryType,
        decimal   amount,
        string?   note,
        Guid      currencyId,
        decimal?  exchangeRate,
        decimal?  exchangeRateTo,
        Guid?     accountId,
        Guid?     paymentMethodId,
        DateOnly? operationDate = null)
    {
        return new ExecutionRecord
        {
            BudgetId        = budgetId,
            PeriodId        = periodId,
            BudgetLineId    = budgetLineId,
            EntryType       = entryType,
            Amount          = amount,
            Note            = note,
            CurrencyId      = currencyId,
            ExchangeRate    = exchangeRate,
            ExchangeRateTo  = exchangeRateTo,
            AccountId       = accountId,
            PaymentMethodId = paymentMethodId,
            OperationDate   = operationDate,
        };
    }

    public void Update(
        EntryType entryType,
        decimal   amount,
        string?   note,
        Guid      currencyId,
        decimal?  exchangeRate,
        decimal?  exchangeRateTo,
        Guid?     accountId,
        Guid?     paymentMethodId,
        DateOnly? operationDate = null)
    {
        EntryType       = entryType;
        Amount          = amount;
        Note            = note;
        CurrencyId      = currencyId;
        ExchangeRate    = exchangeRate;
        ExchangeRateTo  = exchangeRateTo;
        AccountId       = accountId;
        PaymentMethodId = paymentMethodId;
        OperationDate   = operationDate;
        UpdatedAt       = DateTimeOffset.UtcNow;
    }

    public void SoftDelete() => DeletedAt = DateTimeOffset.UtcNow;

    public void Restore()
    {
        DeletedAt = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
