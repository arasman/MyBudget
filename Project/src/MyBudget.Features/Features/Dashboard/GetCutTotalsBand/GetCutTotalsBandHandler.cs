using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Dashboard.GetCutTotalsBand;

/// <summary>
/// Dapper read — DASH-2/DASH-3/DASH-11 lifetime average band.
///
/// Stage 1 (SQL): CutRecord has no PeriodId FK, so each cut is attached to a Period by
/// date containment — reusing the active_period join technique from
/// BudgetExecutionSummaryQuery (CurrentSituation/Shared). Cuts whose CutDate falls outside
/// every Period's date range are excluded here (DASH-11); GetLifetimeCutTotals (DASH-1) is
/// untouched and keeps them. Totals are then averaged WITHIN each period (GROUP BY PeriodId).
///
/// Stage 2 (C#): AVG/MIN/MAX of those per-period averages, computed ACROSS periods, via
/// CutTotalsBandCalculator (Decision 4 — never a flat average across individual cuts).
/// Kept out of SQL to avoid a ~50-column UNION ALL with per-branch NULL casts; the stage-2
/// input is already the small period-averaged set (at most one row per Period), so computing
/// it in C# is O(periods) and trivially unit-testable in isolation (see
/// CutTotalsBandCalculatorTests), matching this codebase's existing convention of mapping a
/// flat Dapper row set into a richer C#-computed shape (see ListPeriodExecutionTotalsHandler).
/// </summary>
public sealed class GetCutTotalsBandHandler
    : IRequestHandler<GetCutTotalsBandQuery, Result<CutTotalsBandResponse>>
{
    private const string ConversionBasis = "cut-frozen";

    private readonly ConnectionFactory _factory;

    public GetCutTotalsBandHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<CutTotalsBandResponse>> Handle(
        GetCutTotalsBandQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        const string sql = """
            WITH period_cuts AS (
                SELECT
                    p."Id"                     AS "PeriodId",
                    p."StartDate"               AS "PeriodStart",
                    p."EndDate"                 AS "PeriodEnd",
                    cr."TotalPositive",         cr."TotalPositiveAlt",
                    cr."TotalNegative",         cr."TotalNegativeAlt",
                    cr."TotalDeudaEnCurso",     cr."TotalDeudaEnCursoAlt",
                    cr."TotalBudgeted",         cr."TotalBudgetedAlt",
                    cr."TotalRegistered",       cr."TotalRegisteredAlt",
                    cr."Remaining",             cr."RemainingAlt",
                    cr."TotalAvailable",        cr."TotalAvailableAlt",
                    cr."TotalNet",              cr."TotalNetAlt"
                FROM "CutRecords" cr
                JOIN "Cycles" cy ON cy."BudgetId" = cr."BudgetId" AND cy."DeletedAt" IS NULL
                JOIN "Periods" p ON p."CycleId" = cy."Id"
                    AND p."DeletedAt" IS NULL
                    AND cr."CutDate" BETWEEN p."StartDate" AND p."EndDate"
                WHERE cr."BudgetId" = @BudgetId
            )
            SELECT
                "PeriodId"                          AS "PeriodId",
                MIN("PeriodStart")                  AS "PeriodStart",
                MIN("PeriodEnd")                     AS "PeriodEnd",
                AVG("TotalPositive")                AS "AvgTotalPositive",
                AVG("TotalPositiveAlt")              AS "AvgTotalPositiveAlt",
                AVG("TotalNegative")                AS "AvgTotalNegative",
                AVG("TotalNegativeAlt")              AS "AvgTotalNegativeAlt",
                AVG("TotalDeudaEnCurso")            AS "AvgTotalDeudaEnCurso",
                AVG("TotalDeudaEnCursoAlt")         AS "AvgTotalDeudaEnCursoAlt",
                AVG("TotalBudgeted")                AS "AvgTotalBudgeted",
                AVG("TotalBudgetedAlt")              AS "AvgTotalBudgetedAlt",
                AVG("TotalRegistered")              AS "AvgTotalRegistered",
                AVG("TotalRegisteredAlt")            AS "AvgTotalRegisteredAlt",
                AVG("Remaining")                    AS "AvgRemaining",
                AVG("RemainingAlt")                  AS "AvgRemainingAlt",
                AVG("TotalAvailable")               AS "AvgTotalAvailable",
                AVG("TotalAvailableAlt")             AS "AvgTotalAvailableAlt",
                AVG("TotalNet")                     AS "AvgTotalNet",
                AVG("TotalNetAlt")                   AS "AvgTotalNetAlt"
            FROM period_cuts
            GROUP BY "PeriodId"
            ORDER BY "PeriodStart" ASC
            """;

        var rows = await conn.QueryAsync<PeriodAverageRow>(sql, new { query.BudgetId });

        var periods = rows
            .Select(r => new PeriodAverageDto(
                r.PeriodId, r.PeriodStart, r.PeriodEnd,
                new ConceptTotalsDto(
                    r.AvgTotalPositive,        r.AvgTotalPositiveAlt,
                    r.AvgTotalNegative,        r.AvgTotalNegativeAlt,
                    r.AvgTotalDeudaEnCurso,    r.AvgTotalDeudaEnCursoAlt,
                    r.AvgTotalBudgeted,        r.AvgTotalBudgetedAlt,
                    r.AvgTotalRegistered,      r.AvgTotalRegisteredAlt,
                    r.AvgRemaining,            r.AvgRemainingAlt,
                    r.AvgTotalAvailable,       r.AvgTotalAvailableAlt,
                    r.AvgTotalNet,             r.AvgTotalNetAlt)))
            .ToList();

        var band = CutTotalsBandCalculator.Compute(periods);

        return Result<CutTotalsBandResponse>.Success(
            new CutTotalsBandResponse(ConversionBasis, periods.Count, periods, band));
    }

    private sealed record PeriodAverageRow(
        Guid     PeriodId,
        DateOnly PeriodStart,
        DateOnly PeriodEnd,
        decimal  AvgTotalPositive,        decimal AvgTotalPositiveAlt,
        decimal  AvgTotalNegative,        decimal AvgTotalNegativeAlt,
        decimal  AvgTotalDeudaEnCurso,    decimal AvgTotalDeudaEnCursoAlt,
        decimal  AvgTotalBudgeted,        decimal AvgTotalBudgetedAlt,
        decimal  AvgTotalRegistered,      decimal AvgTotalRegisteredAlt,
        decimal  AvgRemaining,            decimal AvgRemainingAlt,
        decimal  AvgTotalAvailable,       decimal AvgTotalAvailableAlt,
        decimal  AvgTotalNet,             decimal AvgTotalNetAlt);
}
