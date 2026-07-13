using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.ListExecutionRecords;

/// <summary>
/// Dapper read — returns all non-deleted ExecutionRecords for a BudgetLine,
/// ordered by CreatedAt ASC. Verifies BudgetLine belongs to the given Period and Budget.
/// </summary>
public sealed class ListExecutionRecordsHandler
    : IRequestHandler<ListExecutionRecordsQuery, Result<IReadOnlyList<ExecutionRecordDto>>>
{
    private readonly ConnectionFactory _factory;

    public ListExecutionRecordsHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<IReadOnlyList<ExecutionRecordDto>>> Handle(
        ListExecutionRecordsQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // Verify BudgetLine exists, belongs to Period and Budget, and is not deleted
        var lineExists = await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM "BudgetLines" bl
                JOIN "Periods" p   ON p."Id" = bl."PeriodId"
                JOIN "Cycles"  c   ON c."Id" = p."CycleId"
                WHERE bl."Id"       = @BudgetLineId
                  AND bl."PeriodId" = @PeriodId
                  AND c."BudgetId"  = @BudgetId
                  AND bl."DeletedAt" IS NULL
            )
            """,
            new { query.BudgetLineId, query.PeriodId, query.BudgetId });

        if (!lineExists)
            return Result<IReadOnlyList<ExecutionRecordDto>>.Failure("BUDGET_LINE_NOT_FOUND");

        var rows = await conn.QueryAsync<ExecutionRecordRow>(
            """
            SELECT e."Id",
                   e."EntryType",
                   e."Amount",
                   e."CurrencyId",
                   e."ExchangeRate",
                   e."ExchangeRateTo",
                   e."AccountId",
                   e."PaymentMethodId",
                   e."Note",
                   e."CreatedAt",
                   e."UpdatedAt"
            FROM "ExecutionRecords" e
            WHERE e."BudgetLineId" = @BudgetLineId
              AND e."DeletedAt" IS NULL
            ORDER BY e."CreatedAt" ASC
            """,
            new { query.BudgetLineId });

        var items = rows
            .Select(r => new ExecutionRecordDto(
                r.Id,
                r.EntryType,
                r.Amount,
                r.CurrencyId,
                r.ExchangeRate,
                r.ExchangeRateTo,
                r.AccountId,
                r.PaymentMethodId,
                r.Note,
                new DateTimeOffset(r.CreatedAt, TimeSpan.Zero),
                r.UpdatedAt.HasValue ? new DateTimeOffset(r.UpdatedAt.Value, TimeSpan.Zero) : null))
            .ToList();

        return Result<IReadOnlyList<ExecutionRecordDto>>.Success(items);
    }

    // Dapper uses DateTime (not DateTimeOffset) with Npgsql
    private sealed record ExecutionRecordRow(
        Guid      Id,
        int       EntryType,
        decimal   Amount,
        Guid      CurrencyId,
        decimal?  ExchangeRate,
        decimal?  ExchangeRateTo,
        Guid?     AccountId,
        Guid?     PaymentMethodId,
        string?   Note,
        DateTime  CreatedAt,
        DateTime? UpdatedAt);
}
