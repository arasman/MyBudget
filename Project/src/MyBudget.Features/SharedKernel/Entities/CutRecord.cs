namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Periodic financial snapshot header for a budget.
/// UNIQUE(BudgetId, CutDate) enforced at DB level.
/// ProjectionsJson is a nullable placeholder — no schema defined yet.
/// </summary>
public sealed class CutRecord : BaseEntity, IAuditableEntity
{
    public Guid      BudgetId        { get; private set; }
    public DateOnly  CutDate         { get; private set; }
    public decimal   ExchangeRate    { get; private set; }
    public string?   ProjectionsJson { get; private set; }

    // CS-6: 16 persisted totals (8 concepts × primary/alternate), frozen at save time.
    public decimal TotalPositive         { get; private set; }
    public decimal TotalPositiveAlt      { get; private set; }
    public decimal TotalNegative         { get; private set; }
    public decimal TotalNegativeAlt      { get; private set; }
    public decimal TotalDeudaEnCurso     { get; private set; }
    public decimal TotalDeudaEnCursoAlt  { get; private set; }
    public decimal TotalBudgeted         { get; private set; }
    public decimal TotalBudgetedAlt      { get; private set; }
    public decimal TotalRegistered       { get; private set; }
    public decimal TotalRegisteredAlt    { get; private set; }
    public decimal Remaining             { get; private set; }
    public decimal RemainingAlt          { get; private set; }
    public decimal TotalAvailable        { get; private set; }
    public decimal TotalAvailableAlt     { get; private set; }
    public decimal TotalNet              { get; private set; }
    public decimal TotalNetAlt           { get; private set; }

    // Navigation
    public ICollection<CutBankAccount> CutBankAccounts { get; private set; } = new List<CutBankAccount>();

    private CutRecord() { }

    public Guid? ResolveBudgetId() => BudgetId;

    public static CutRecord Create(
        Guid       budgetId,
        DateOnly   cutDate,
        decimal    exchangeRate,
        CutTotals  totals,
        string?    projectionsJson = null)
    {
        return new CutRecord
        {
            BudgetId        = budgetId,
            CutDate         = cutDate,
            ExchangeRate    = exchangeRate,
            ProjectionsJson = projectionsJson,

            TotalPositive        = totals.TotalPositive,
            TotalPositiveAlt     = totals.TotalPositiveAlt,
            TotalNegative        = totals.TotalNegative,
            TotalNegativeAlt     = totals.TotalNegativeAlt,
            TotalDeudaEnCurso    = totals.TotalDeudaEnCurso,
            TotalDeudaEnCursoAlt = totals.TotalDeudaEnCursoAlt,
            TotalBudgeted        = totals.TotalBudgeted,
            TotalBudgetedAlt     = totals.TotalBudgetedAlt,
            TotalRegistered      = totals.TotalRegistered,
            TotalRegisteredAlt   = totals.TotalRegisteredAlt,
            Remaining            = totals.Remaining,
            RemainingAlt         = totals.RemainingAlt,
            TotalAvailable       = totals.TotalAvailable,
            TotalAvailableAlt    = totals.TotalAvailableAlt,
            TotalNet             = totals.TotalNet,
            TotalNetAlt          = totals.TotalNetAlt,
        };
    }

    public void Update(decimal exchangeRate, CutTotals totals, string? projectionsJson = null)
    {
        ExchangeRate    = exchangeRate;
        ProjectionsJson = projectionsJson;

        TotalPositive        = totals.TotalPositive;
        TotalPositiveAlt     = totals.TotalPositiveAlt;
        TotalNegative        = totals.TotalNegative;
        TotalNegativeAlt     = totals.TotalNegativeAlt;
        TotalDeudaEnCurso    = totals.TotalDeudaEnCurso;
        TotalDeudaEnCursoAlt = totals.TotalDeudaEnCursoAlt;
        TotalBudgeted        = totals.TotalBudgeted;
        TotalBudgetedAlt     = totals.TotalBudgetedAlt;
        TotalRegistered      = totals.TotalRegistered;
        TotalRegisteredAlt   = totals.TotalRegisteredAlt;
        Remaining            = totals.Remaining;
        RemainingAlt         = totals.RemainingAlt;
        TotalAvailable       = totals.TotalAvailable;
        TotalAvailableAlt    = totals.TotalAvailableAlt;
        TotalNet             = totals.TotalNet;
        TotalNetAlt          = totals.TotalNetAlt;

        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
