using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Dashboard.GetBudgetLineSeries;

/// <summary>
/// Dapper read — DASH-4/5/6 per-BudgetLine per-Period series. Generalizes
/// ListPeriodExecutionTotalsHandler's budgeted-revision LATERAL join and net formula
/// (Expense + DebitNote - CreditNote) to accept multiple PeriodIds (ANY(@PeriodIds), which
/// may span one or two Cycles) and multiple BudgetLineIds (ANY(@LineIds)). Within-cycle and
/// cross-cycle comparisons are the SAME query — only the PeriodIds the client sends differ
/// (design.md non-obvious SQL constraints). Cross-cycle identity is BudgetLineId alone:
/// BudgetLine is BudgetId-scoped, not CycleId-scoped, so no fuzzy matching is needed
/// (design.md Decision 3).
///
/// Values use ExecutionRecord's transaction-time ExchangeRate — conversionBasis is always
/// "transaction-time" (DASH-9 basis labeling), never blended with CutRecord's cut-frozen
/// rate used by GetLifetimeCutTotals/GetCutTotalsBand.
///
/// DASH-12: each returned period carries its Cycle's DefaultCurrencyId (not Budget's) so a
/// cross-cycle selection can carry two different currencies for the client mismatch guard.
/// Split into two queries on the same connection (period metadata, then per-line/per-period
/// rows) rather than one UNION — both are independently simple and testable; the second
/// reuses the periods_data CTE technique from GetCutTotalsBandHandler / BudgetExecutionSummaryQuery.
/// </summary>
public sealed class GetBudgetLineSeriesHandler
    : IRequestHandler<GetBudgetLineSeriesQuery, Result<BudgetLineSeriesResponse>>
{
    private const string ConversionBasis = "transaction-time";

    private readonly ConnectionFactory _factory;

    public GetBudgetLineSeriesHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<BudgetLineSeriesResponse>> Handle(
        GetBudgetLineSeriesQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // DASH-12: period metadata incl. Cycle.DefaultCurrencyId per selected period.
        const string periodsSql = """
            SELECT
                p."Id"                 AS "PeriodId",
                p."CycleId"             AS "CycleId",
                p."StartDate"           AS "PeriodStart",
                cy."DefaultCurrencyId"  AS "DefaultCurrencyId"
            FROM "Periods" p
            JOIN "Cycles" cy ON cy."Id" = p."CycleId"
            WHERE p."Id"        = ANY(@PeriodIds)
              AND cy."BudgetId" = @BudgetId
              AND p."DeletedAt"  IS NULL
              AND cy."DeletedAt" IS NULL
            ORDER BY p."StartDate" ASC
            """;

        var periods = (await conn.QueryAsync<PeriodSeriesDto>(
            periodsSql, new { query.BudgetId, query.PeriodIds })).ToList();

        // DASH-4/5/6: per-BudgetLine per-Period budgeted/registered totals.
        const string rowsSql = """
            WITH periods_data AS (
                SELECT
                    p."Id"                 AS "PeriodId",
                    p."StartDate"           AS "PeriodStart",
                    p."EndDate"             AS "PeriodEnd",
                    cy."DefaultCurrencyId"  AS "DefaultCurrencyId"
                FROM "Periods" p
                JOIN "Cycles" cy ON cy."Id" = p."CycleId"
                WHERE p."Id"        = ANY(@PeriodIds)
                  AND cy."BudgetId" = @BudgetId
                  AND p."DeletedAt"  IS NULL
                  AND cy."DeletedAt" IS NULL
            ),
            converted AS (
                SELECT
                    e."BudgetLineId",
                    e."PeriodId",
                    e."EntryType",
                    CASE
                        WHEN e."CurrencyId" = pd."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                            THEN e."Amount"
                        ELSE e."Amount" * e."ExchangeRate"
                    END AS "ConvertedAmount"
                FROM "ExecutionRecords" e
                JOIN periods_data pd ON pd."PeriodId" = e."PeriodId"
                WHERE e."BudgetId"     = @BudgetId
                  AND e."BudgetLineId" = ANY(@LineIds)
                  AND e."DeletedAt"    IS NULL
            )
            SELECT
                bl."Id"                            AS "BudgetLineId",
                bl."Name"                           AS "BudgetLineName",
                pd."PeriodId"                       AS "PeriodId",
                COALESCE(rev."BudgetedAmount", 0)   AS "BudgetedAmount",
                COALESCE(SUM(CASE WHEN c."EntryType" = 1 THEN c."ConvertedAmount" ELSE 0 END), 0)
                  + COALESCE(SUM(CASE WHEN c."EntryType" = 3 THEN c."ConvertedAmount" ELSE 0 END), 0)
                  - COALESCE(SUM(CASE WHEN c."EntryType" = 2 THEN c."ConvertedAmount" ELSE 0 END), 0)
                    AS "NetTotal"
            FROM "BudgetLines" bl
            CROSS JOIN periods_data pd
            LEFT JOIN LATERAL (
                SELECT r."BudgetedAmount"
                FROM "BudgetLineRevisions" r
                WHERE r."BudgetLineId" = bl."Id"
                  AND r."ValidFrom"::date <= pd."PeriodStart"
                  AND (r."ValidTo" IS NULL OR r."ValidTo"::date >= pd."PeriodStart")
                LIMIT 1
            ) rev ON true
            LEFT JOIN converted c ON c."BudgetLineId" = bl."Id" AND c."PeriodId" = pd."PeriodId"
            WHERE bl."Id"        = ANY(@LineIds)
              AND bl."BudgetId"  = @BudgetId
              AND bl."DeletedAt" IS NULL
              AND bl."StartDate"::date <= pd."PeriodEnd"
              AND (bl."EndDate" IS NULL OR bl."EndDate"::date >= pd."PeriodStart")
            GROUP BY bl."Id", bl."Name", pd."PeriodId", pd."PeriodStart", rev."BudgetedAmount"
            ORDER BY pd."PeriodStart" ASC, bl."Name" ASC
            """;

        var rows = (await conn.QueryAsync<BudgetLineSeriesRowDto>(
            rowsSql, new { query.BudgetId, query.PeriodIds, query.LineIds })).ToList();

        return Result<BudgetLineSeriesResponse>.Success(
            new BudgetLineSeriesResponse(ConversionBasis, periods, rows));
    }
}
