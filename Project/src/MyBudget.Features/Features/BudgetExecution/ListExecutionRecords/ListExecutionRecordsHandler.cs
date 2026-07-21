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

        // Verify BudgetLine exists, belongs to the Budget, and is not deleted.
        // Also verify the Period belongs to the same Budget (via Cycle).
        // BudgetLine is no longer FK-linked to Period — relationship is via date-range intersection.
        var lineExists = await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM "BudgetLines" bl
                WHERE bl."Id"       = @BudgetLineId
                  AND bl."BudgetId" = @BudgetId
                  AND bl."DeletedAt" IS NULL
            )
            AND EXISTS (
                SELECT 1
                FROM "Periods" p
                JOIN "Cycles" c ON c."Id" = p."CycleId"
                WHERE p."Id"       = @PeriodId
                  AND c."BudgetId" = @BudgetId
                  AND p."DeletedAt" IS NULL
            )
            """,
            new { query.BudgetLineId, query.PeriodId, query.BudgetId });

        if (!lineExists)
            return Result<IReadOnlyList<ExecutionRecordDto>>.Failure("BUDGET_LINE_NOT_FOUND");

        var sql = query.IncludeDeleted
            ? """
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
                     e."UpdatedAt",
                     e."DeletedAt",
                     e."OperationDate"
              FROM "ExecutionRecords" e
              WHERE e."BudgetLineId" = @BudgetLineId
              ORDER BY e."CreatedAt" ASC
              """
            : """
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
                     e."UpdatedAt",
                     e."DeletedAt",
                     e."OperationDate"
              FROM "ExecutionRecords" e
              WHERE e."BudgetLineId" = @BudgetLineId
                AND e."DeletedAt" IS NULL
              ORDER BY e."CreatedAt" ASC
              """;

        var rows = await conn.QueryAsync<ExecutionRecordRow>(sql, new { query.BudgetLineId });

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
                r.CreatedAt,
                r.UpdatedAt,
                r.DeletedAt,
                r.OperationDate))
            .ToList();

        return Result<IReadOnlyList<ExecutionRecordDto>>.Success(items);
    }

    // Npgsql 10: 'timestamp with time zone' columns → DateTimeOffset; 'date' columns → DateOnly
    private sealed class ExecutionRecordRow
    {
        public Guid             Id              { get; init; }
        public int              EntryType       { get; init; }
        public decimal          Amount          { get; init; }
        public Guid             CurrencyId      { get; init; }
        public decimal?         ExchangeRate    { get; init; }
        public decimal?         ExchangeRateTo  { get; init; }
        public Guid?            AccountId       { get; init; }
        public Guid?            PaymentMethodId { get; init; }
        public string?          Note            { get; init; }
        public DateTimeOffset   CreatedAt       { get; init; }
        public DateTimeOffset?  UpdatedAt       { get; init; }
        public DateTimeOffset?  DeletedAt       { get; init; }
        public DateOnly?        OperationDate   { get; init; }
    }
}
