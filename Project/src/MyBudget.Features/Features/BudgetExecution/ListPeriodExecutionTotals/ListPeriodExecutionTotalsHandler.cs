using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.ListPeriodExecutionTotals;

/// <summary>
/// Dapper read — UNION ALL query returning per-BudgetLine and per-Category totals.
/// Currency conversion: Amount * ExchangeRate when CurrencyId != Cycle.DefaultCurrencyId.
/// Discriminator column GroupLevel: 'Line' | 'Category'.
/// REQ-EXEC-TOTALS-1 to REQ-EXEC-TOTALS-4.
///
/// PR2b change (REQ-EXEC-TOTALS-1):
///   - BudgetLines filtered via date-range intersection (no PeriodId FK).
///   - Effective revision selected via ValidFrom/ValidTo (not RevisedAt).
/// </summary>
public sealed class ListPeriodExecutionTotalsHandler
    : IRequestHandler<ListPeriodExecutionTotalsQuery, Result<PeriodExecutionTotalsResponse>>
{
    private readonly ConnectionFactory _factory;

    public ListPeriodExecutionTotalsHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<PeriodExecutionTotalsResponse>> Handle(
        ListPeriodExecutionTotalsQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // REQ-EXEC-TOTALS-2: netAmount = Expenses + DebitNotes - CreditNotes
        // REQ-EXEC-TOTALS-4: currency conversion Amount * ExchangeRate when != DefaultCurrency
        // REQ-EXEC-TOTALS-1 (PR2b): BudgetLines via date-range intersection; revision via ValidFrom/ValidTo
        // Rows: GroupLevel = 'Line' or 'Category'
        const string sql = """
            WITH period_data AS (
                SELECT
                    p."StartDate"               AS "PeriodStart",
                    p."EndDate"                 AS "PeriodEnd",
                    cy."DefaultCurrencyId"       AS "DefaultCurrencyId"
                FROM "Periods" p
                JOIN "Cycles" cy ON cy."Id" = p."CycleId"
                WHERE p."Id"         = @PeriodId
                  AND cy."BudgetId"  = @BudgetId
                  AND p."DeletedAt"  IS NULL
                  AND cy."DeletedAt" IS NULL
                LIMIT 1
            ),
            converted AS (
                SELECT
                    e."BudgetLineId",
                    e."EntryType",
                    CASE
                        WHEN e."CurrencyId" = pd."DefaultCurrencyId" OR e."ExchangeRate" IS NULL
                            THEN e."Amount"
                        ELSE e."Amount" * e."ExchangeRate"
                    END AS "ConvertedAmount"
                FROM "ExecutionRecords" e
                CROSS JOIN period_data pd
                WHERE e."PeriodId"  = @PeriodId
                  AND e."BudgetId"  = @BudgetId
                  AND e."DeletedAt" IS NULL
            )

            -- Part 1: per-BudgetLine totals
            SELECT
                'Line'                       AS "GroupLevel",
                bl."Id"                      AS "BudgetLineId",
                bl."Name"                    AS "BudgetLineName",
                COALESCE(rev."BudgetedAmount", 0)  AS "BudgetedAmount",
                rev."CurrencyId"             AS "BudgetedCurrencyId",
                NULL::uuid                   AS "CategoryGroupId",
                NULL                         AS "CategoryGroupName",
                NULL::uuid                   AS "CategoryId",
                NULL                         AS "CategoryName",
                COALESCE(SUM(CASE WHEN c."EntryType" = 1 THEN c."ConvertedAmount" ELSE 0 END), 0) AS "TotalExpenses",
                COALESCE(SUM(CASE WHEN c."EntryType" = 2 THEN c."ConvertedAmount" ELSE 0 END), 0) AS "TotalCreditNotes",
                COALESCE(SUM(CASE WHEN c."EntryType" = 3 THEN c."ConvertedAmount" ELSE 0 END), 0) AS "TotalDebitNotes"
            FROM "BudgetLines" bl
            CROSS JOIN period_data pd
            LEFT JOIN LATERAL (
                SELECT r."BudgetedAmount", r."CurrencyId"
                FROM "BudgetLineRevisions" r
                WHERE r."BudgetLineId" = bl."Id"
                  AND r."ValidFrom"    <= pd."PeriodStart"
                  AND (r."ValidTo" IS NULL OR r."ValidTo" >= pd."PeriodStart")
                LIMIT 1
            ) rev ON true
            LEFT JOIN converted c ON c."BudgetLineId" = bl."Id"
            WHERE bl."BudgetId"   = @BudgetId
              AND bl."StartDate"  <= pd."PeriodEnd"
              AND (bl."EndDate" IS NULL OR bl."EndDate" >= pd."PeriodStart")
              AND bl."DeletedAt"  IS NULL
            GROUP BY bl."Id", bl."Name", rev."BudgetedAmount", rev."CurrencyId"

            UNION ALL

            -- Part 2: per-CategoryGroup/Category totals
            SELECT
                'Category'                   AS "GroupLevel",
                NULL::uuid                   AS "BudgetLineId",
                NULL                         AS "BudgetLineName",
                0                            AS "BudgetedAmount",
                NULL::uuid                   AS "BudgetedCurrencyId",
                cg."Id"                      AS "CategoryGroupId",
                cg."Name"                    AS "CategoryGroupName",
                cat."Id"                     AS "CategoryId",
                cat."Name"                   AS "CategoryName",
                COALESCE(SUM(CASE WHEN c."EntryType" = 1 THEN c."ConvertedAmount" ELSE 0 END), 0) AS "TotalExpenses",
                COALESCE(SUM(CASE WHEN c."EntryType" = 2 THEN c."ConvertedAmount" ELSE 0 END), 0) AS "TotalCreditNotes",
                COALESCE(SUM(CASE WHEN c."EntryType" = 3 THEN c."ConvertedAmount" ELSE 0 END), 0) AS "TotalDebitNotes"
            FROM "BudgetLines" bl
            CROSS JOIN period_data pd
            JOIN "CategoryGroups" cg ON cg."Id" = bl."CategoryGroupId"
            LEFT JOIN "Categories" cat ON cat."Id" = bl."CategoryId"
            LEFT JOIN converted c ON c."BudgetLineId" = bl."Id"
            WHERE bl."BudgetId"   = @BudgetId
              AND bl."StartDate"  <= pd."PeriodEnd"
              AND (bl."EndDate" IS NULL OR bl."EndDate" >= pd."PeriodStart")
              AND bl."DeletedAt"  IS NULL
            GROUP BY cg."Id", cg."Name", cat."Id", cat."Name"
            """;

        var rows = await conn.QueryAsync<TotalsRow>(sql, new { query.PeriodId, query.BudgetId });

        var lineRows     = new List<LineTotalDto>();
        var categoryRows = new List<CategoryTotalDto>();

        foreach (var row in rows)
        {
            if (row.GroupLevel == "Line")
            {
                lineRows.Add(new LineTotalDto(
                    row.BudgetLineId!.Value,
                    row.BudgetLineName!,
                    row.BudgetedAmount,
                    row.TotalExpenses,
                    row.TotalCreditNotes,
                    row.TotalDebitNotes,
                    row.TotalExpenses + row.TotalDebitNotes - row.TotalCreditNotes));
            }
            else
            {
                categoryRows.Add(new CategoryTotalDto(
                    row.CategoryGroupId!.Value,
                    row.CategoryGroupName!,
                    row.CategoryId,
                    row.CategoryName,
                    row.TotalExpenses,
                    row.TotalCreditNotes,
                    row.TotalDebitNotes,
                    row.TotalExpenses + row.TotalDebitNotes - row.TotalCreditNotes));
            }
        }

        return Result<PeriodExecutionTotalsResponse>.Success(
            new PeriodExecutionTotalsResponse(lineRows, categoryRows));
    }

    private sealed record TotalsRow(
        string   GroupLevel,
        Guid?    BudgetLineId,
        string?  BudgetLineName,
        decimal  BudgetedAmount,
        Guid?    BudgetedCurrencyId,
        Guid?    CategoryGroupId,
        string?  CategoryGroupName,
        Guid?    CategoryId,
        string?  CategoryName,
        decimal  TotalExpenses,
        decimal  TotalCreditNotes,
        decimal  TotalDebitNotes);
}
