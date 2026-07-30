using Dapper;
using System.Data;

namespace MyBudget.Features.Features.CurrentSituation.Shared;

/// <summary>
/// Budget execution summary for the active period covering a cut date.
/// Result of the CTE extracted verbatim from the original GetCutRecordHandler
/// (design.md Decision 3) — shared by UpsertCutRecordHandler (compute-at-write)
/// and GetCutRecordHandler's draft path (live compute).
/// </summary>
public sealed record BudgetExecutionSummary(
    decimal TotalBudgeted,
    decimal TotalRegistered,
    decimal Remaining)
{
    public static readonly BudgetExecutionSummary Zero = new(0m, 0m, 0m);
}

/// <summary>
/// Dapper query for the budget execution summary (TotalBudgeted, TotalRegistered,
/// Remaining) of the active period covering a given cut date. Returns
/// <see cref="BudgetExecutionSummary.Zero"/> when no active period covers the date.
/// </summary>
public static class BudgetExecutionSummaryQuery
{
    private const string ExecutionSql = """
        WITH active_period AS (
            SELECT
                p."Id"                    AS "PeriodId",
                p."StartDate"             AS "PeriodStart",
                p."EndDate"               AS "PeriodEnd",
                cy."DefaultCurrencyId"    AS "DefaultCurrencyId"
            FROM "Periods" p
            JOIN "Cycles" cy ON cy."Id" = p."CycleId"
            WHERE cy."BudgetId"  = @BudgetId
              AND cy."DeletedAt" IS NULL
              AND p."DeletedAt"  IS NULL
              AND p."IsClosed"   = false
              AND p."StartDate"  <= @CutDate
              AND p."EndDate"    >= @CutDate
            LIMIT 1
        ),
        budgeted AS (
            SELECT COALESCE(SUM(rev."BudgetedAmount"), 0) AS "TotalBudgeted"
            FROM "BudgetLines" bl
            JOIN active_period ap ON true
            LEFT JOIN LATERAL (
                SELECT r."BudgetedAmount"
                FROM "BudgetLineRevisions" r
                WHERE r."BudgetLineId" = bl."Id"
                  AND r."ValidFrom"::date <= ap."PeriodStart"
                  AND (r."ValidTo" IS NULL OR r."ValidTo"::date >= ap."PeriodStart")
                LIMIT 1
            ) rev ON true
            WHERE bl."BudgetId"  = @BudgetId
              AND bl."DeletedAt" IS NULL
              AND bl."StartDate"::date <= ap."PeriodEnd"
              AND (bl."EndDate" IS NULL OR bl."EndDate"::date >= ap."PeriodStart")
        ),
        registered AS (
            SELECT COALESCE(SUM(
                CASE
                    WHEN e."EntryType" = 1 THEN  -- Expense
                        CASE WHEN e."CurrencyId" = ap."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                            THEN e."Amount"
                            ELSE e."Amount" * e."ExchangeRate"
                        END
                    WHEN e."EntryType" = 3 THEN  -- DebitNote
                        CASE WHEN e."CurrencyId" = ap."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                            THEN e."Amount"
                            ELSE e."Amount" * e."ExchangeRate"
                        END
                    WHEN e."EntryType" = 2 THEN  -- CreditNote (subtract)
                        -(CASE WHEN e."CurrencyId" = ap."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                            THEN e."Amount"
                            ELSE e."Amount" * e."ExchangeRate"
                         END)
                    ELSE 0
                END
            ), 0) AS "TotalRegistered"
            FROM "ExecutionRecords" e
            JOIN active_period ap ON ap."PeriodId" = e."PeriodId"
            WHERE e."BudgetId"  = @BudgetId
              AND e."DeletedAt" IS NULL
        )
        SELECT
            b."TotalBudgeted",
            r."TotalRegistered",
            (b."TotalBudgeted" - r."TotalRegistered") AS "Remaining"
        FROM budgeted b
        CROSS JOIN registered r
        """;

    public static async Task<BudgetExecutionSummary> ExecuteAsync(
        IDbConnection conn, Guid budgetId, DateOnly cutDate)
    {
        var row = await conn.QueryFirstOrDefaultAsync<BudgetExecutionSummary>(
            ExecutionSql, new { BudgetId = budgetId, CutDate = cutDate.ToDateTime(TimeOnly.MinValue) });

        return row ?? BudgetExecutionSummary.Zero;
    }
}
