using Dapper;
using Mediator;
using MyBudget.Features.Features.CurrentSituation.Shared;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.CurrentSituation.GetCutRecord;

/// <summary>
/// Dapper read-handler for GetCutRecord (CS-2).
///
/// Existing record path: reads the 16 persisted total columns and the execution summary
/// verbatim from storage — no bank-account aggregation, no execution-summary CTE (CS-2, CS-6).
/// Draft path: LEFT JOINs active BankAccounts against the last cut's balances for cloning,
/// then computes totals live via the shared BudgetExecutionSummaryQuery + CutTotalsCalculator
/// (unchanged behavior — same components used at upsert time).
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
                cr."Id"                   AS "Id",
                cr."ExchangeRate"         AS "ExchangeRate",
                cr."ProjectionsJson"      AS "ProjectionsJson",
                cr."TotalPositive"        AS "TotalPositive",
                cr."TotalPositiveAlt"     AS "TotalPositiveAlt",
                cr."TotalNegative"        AS "TotalNegative",
                cr."TotalNegativeAlt"     AS "TotalNegativeAlt",
                cr."TotalDeudaEnCurso"    AS "TotalDeudaEnCurso",
                cr."TotalDeudaEnCursoAlt" AS "TotalDeudaEnCursoAlt",
                cr."TotalBudgeted"        AS "TotalBudgeted",
                cr."TotalBudgetedAlt"     AS "TotalBudgetedAlt",
                cr."TotalRegistered"      AS "TotalRegistered",
                cr."TotalRegisteredAlt"   AS "TotalRegisteredAlt",
                cr."Remaining"            AS "Remaining",
                cr."RemainingAlt"         AS "RemainingAlt",
                cr."TotalAvailable"       AS "TotalAvailable",
                cr."TotalAvailableAlt"    AS "TotalAvailableAlt",
                cr."TotalNet"             AS "TotalNet",
                cr."TotalNetAlt"          AS "TotalNetAlt"
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

        // ── Step 3: load accounts (existing or draft) + totals ────────────
        IReadOnlyList<CutBankAccountDto> accounts;
        bool isDraft;
        Guid? cutRecordId;
        decimal exchangeRate;
        string? projectionsJson;
        BudgetExecutionSummaryDto summaryDto;
        CutTotalsDto totals;

        if (header is not null)
        {
            // Existing record — read persisted totals verbatim, no aggregation/CTE (CS-2, CS-6).
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

            summaryDto = new BudgetExecutionSummaryDto(
                header.TotalBudgeted,
                header.TotalRegistered,
                header.Remaining);

            totals = new CutTotalsDto(
                header.TotalPositive,
                header.TotalNegative,
                header.TotalDeudaEnCurso,
                header.TotalPositiveAlt,
                header.TotalNegativeAlt,
                header.TotalDeudaEnCursoAlt);
        }
        else
        {
            // Draft — clone from last cut or use zero balances; totals computed live.
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

            var executionSummary = await BudgetExecutionSummaryQuery.ExecuteAsync(
                conn, query.BudgetId, query.CutDate);

            summaryDto = new BudgetExecutionSummaryDto(
                executionSummary.TotalBudgeted,
                executionSummary.TotalRegistered,
                executionSummary.Remaining);

            var computed = CutTotalsCalculator.Compute(
                accounts.Select(a => (a.IsPositive, a.BalanceInPrimary)),
                executionSummary,
                exchangeRate);

            totals = new CutTotalsDto(
                computed.TotalPositive,
                computed.TotalNegative,
                computed.TotalDeudaEnCurso,
                computed.TotalPositiveAlt,
                computed.TotalNegativeAlt,
                computed.TotalDeudaEnCursoAlt);
        }

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
        string? ProjectionsJson,
        decimal TotalPositive,
        decimal TotalPositiveAlt,
        decimal TotalNegative,
        decimal TotalNegativeAlt,
        decimal TotalDeudaEnCurso,
        decimal TotalDeudaEnCursoAlt,
        decimal TotalBudgeted,
        decimal TotalBudgetedAlt,
        decimal TotalRegistered,
        decimal TotalRegisteredAlt,
        decimal Remaining,
        decimal RemainingAlt,
        decimal TotalAvailable,
        decimal TotalAvailableAlt,
        decimal TotalNet,
        decimal TotalNetAlt);
}
