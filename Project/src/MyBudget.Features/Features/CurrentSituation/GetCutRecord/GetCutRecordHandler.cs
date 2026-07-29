using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.CurrentSituation.GetCutRecord;

/// <summary>
/// Dapper read-handler for GetCutRecord (CS-2).
///
/// Existing record path: loads persisted CutBankAccount rows, computes totals at query time.
/// Draft path: LEFT JOINs active BankAccounts against the last cut's balances for cloning.
/// Budget execution summary: CTE joining Periods+Cycles, summing BudgetLineRevisions vs ExecutionRecords.
/// CS-6: totals computed from CutBankAccount rows (existing) or Balance=0/cloned draft rows.
/// </summary>
public sealed class GetCutRecordHandler
    : IRequestHandler<GetCutRecordQuery, Result<GetCutRecordResponse>>
{
    private readonly ConnectionFactory _factory;

    public GetCutRecordHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<GetCutRecordResponse>> Handle(
        GetCutRecordQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // ── Step 1: check if a cut record exists for the requested date ──────
        const string cutHeaderSql = """
            SELECT
                cr."Id"              AS "Id",
                cr."ExchangeRate"    AS "ExchangeRate",
                cr."ProjectionsJson" AS "ProjectionsJson"
            FROM "CutRecords" cr
            WHERE cr."BudgetId" = @BudgetId
              AND cr."CutDate"  = @CutDate
            LIMIT 1
            """;

        var header = await conn.QueryFirstOrDefaultAsync<CutHeaderRow>(
            cutHeaderSql, new { query.BudgetId, CutDate = query.CutDate.ToDateTime(TimeOnly.MinValue) });

        // ── Step 2a: primary currency of the covering cycle ──────────────────
        const string primaryCurrencySql = """
            SELECT cy."DefaultCurrencyId"
            FROM "Cycles" cy
            WHERE cy."BudgetId"  = @BudgetId
              AND cy."StartDate" <= @CutDate
              AND cy."EndDate"   >= @CutDate
            LIMIT 1
            """;

        var primaryCurrencyId = await conn.QueryFirstOrDefaultAsync<Guid?>(
            primaryCurrencySql, new { query.BudgetId, CutDate = query.CutDate.ToDateTime(TimeOnly.MinValue) });

        // ── Step 2: budget execution summary (active period at cut date) ─────
        const string executionSql = """
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

        var execSummary = await conn.QueryFirstOrDefaultAsync<ExecutionSummaryRow>(
            executionSql, new { query.BudgetId, CutDate = query.CutDate.ToDateTime(TimeOnly.MinValue) });

        var summaryDto = execSummary is not null
            ? new BudgetExecutionSummaryDto(
                execSummary.TotalBudgeted,
                execSummary.TotalRegistered,
                execSummary.Remaining)
            : new BudgetExecutionSummaryDto(0, 0, 0);

        // ── Step 3: load accounts (existing or draft) ─────────────────────
        IReadOnlyList<CutBankAccountDto> accounts;
        bool isDraft;
        Guid? cutRecordId;
        decimal exchangeRate;
        string? projectionsJson;

        if (header is not null)
        {
            // Existing record — load persisted CutBankAccount rows
            isDraft         = false;
            cutRecordId     = header.Id;
            exchangeRate    = header.ExchangeRate;
            projectionsJson = header.ProjectionsJson;

            const string accountsSql = """
                SELECT
                    cba."BankAccountId",
                    cba."Alias",
                    cba."CurrencyId",
                    cba."IsPositive",
                    cba."DisplayOrder",
                    cba."Balance",
                    cba."BalanceInPrimary"
                FROM "CutBankAccounts" cba
                WHERE cba."CutRecordId" = @CutRecordId
                ORDER BY cba."DisplayOrder" ASC
                """;

            var rows = await conn.QueryAsync<CutBankAccountDto>(
                accountsSql, new { CutRecordId = header.Id });

            accounts = rows.ToList();
        }
        else
        {
            // Draft — clone from last cut or use zero balances
            isDraft         = true;
            cutRecordId     = null;
            exchangeRate    = 1m;
            projectionsJson = null;

            // Find the last cut before requested date
            const string draftSql = """
                WITH last_cut AS (
                    SELECT cr."Id" AS "LastCutId"
                    FROM "CutRecords" cr
                    WHERE cr."BudgetId" = @BudgetId
                      AND cr."CutDate"  < @CutDate
                    ORDER BY cr."CutDate" DESC
                    LIMIT 1
                )
                SELECT
                    ba."Id"          AS "BankAccountId",
                    ba."Alias"       AS "Alias",
                    ba."CurrencyId"  AS "CurrencyId",
                    ba."IsPositive"  AS "IsPositive",
                    ba."DisplayOrder" AS "DisplayOrder",
                    COALESCE(cba."Balance", 0)          AS "Balance",
                    COALESCE(cba."BalanceInPrimary", 0) AS "BalanceInPrimary"
                FROM "BankAccounts" ba
                LEFT JOIN last_cut lc ON true
                LEFT JOIN "CutBankAccounts" cba
                    ON cba."BankAccountId" = ba."Id"
                   AND cba."CutRecordId"  = lc."LastCutId"
                WHERE ba."BudgetId"  = @BudgetId
                  AND ba."DeletedAt" IS NULL
                ORDER BY ba."DisplayOrder" ASC
                """;

            var draftRows = await conn.QueryAsync<CutBankAccountDto>(
                draftSql, new { query.BudgetId, CutDate = query.CutDate.ToDateTime(TimeOnly.MinValue) });

            accounts = draftRows.ToList();
        }

        // ── Step 4: compute totals (CS-6) ────────────────────────────────
        var remaining      = summaryDto.Remaining;
        var totalPositive  = accounts.Where(a => a.IsPositive).Sum(a => a.BalanceInPrimary);
        var totalNegative  = accounts.Where(a => !a.IsPositive).Sum(a => a.BalanceInPrimary);
        var totalDeuda     = remaining + totalNegative;

        // Alt-currency variants (divide by exchange rate; guard division by zero)
        var er             = exchangeRate > 0 ? exchangeRate : 1m;
        var totalPositiveAlt      = totalPositive  / er;
        var totalNegativeAlt      = totalNegative  / er;
        var totalDeudaAlt         = totalDeuda     / er;

        var totals = new CutTotalsDto(
            totalPositive,
            totalNegative,
            totalDeuda,
            totalPositiveAlt,
            totalNegativeAlt,
            totalDeudaAlt);

        return Result<GetCutRecordResponse>.Success(new GetCutRecordResponse(
            isDraft,
            cutRecordId,
            query.CutDate,
            exchangeRate,
            projectionsJson,
            primaryCurrencyId,
            summaryDto,
            accounts,
            totals));
    }

    // ── Private DTOs for Dapper mapping ──────────────────────────────────

    private sealed record CutHeaderRow(
        Guid    Id,
        decimal ExchangeRate,
        string? ProjectionsJson);

    private sealed record ExecutionSummaryRow(
        decimal TotalBudgeted,
        decimal TotalRegistered,
        decimal Remaining);
}
