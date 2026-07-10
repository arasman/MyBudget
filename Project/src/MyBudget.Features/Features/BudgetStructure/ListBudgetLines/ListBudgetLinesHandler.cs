using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListBudgetLines;

/// <summary>
/// Dapper read — verifies period belongs to budget, then returns BudgetLines with latest revision
/// via LATERAL JOIN. Returns 404 if period not found or belongs to a different budget.
/// </summary>
public sealed class ListBudgetLinesHandler
    : IRequestHandler<ListBudgetLinesQuery, Result<IReadOnlyList<BudgetLineResponse>>>
{
    private readonly ConnectionFactory _factory;

    public ListBudgetLinesHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<IReadOnlyList<BudgetLineResponse>>> Handle(
        ListBudgetLinesQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // Verify period belongs to the budget via Period → Cycle chain
        var periodExists = await conn.ExecuteScalarAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1
                FROM "Periods" p
                JOIN "Cycles" c ON c."Id" = p."CycleId"
                WHERE p."Id" = @PeriodId
                  AND c."BudgetId" = @BudgetId
                  AND p."DeletedAt" IS NULL
                  AND c."DeletedAt" IS NULL
            )
            """,
            new { query.PeriodId, query.BudgetId });

        if (!periodExists)
            return Result<IReadOnlyList<BudgetLineResponse>>.Failure("PERIOD_NOT_FOUND");

        var rows = await conn.QueryAsync<BudgetLineRow>(
            """
            SELECT bl."Id", bl."Name", bl."LineType", bl."IsRecurring",
                   bl."CategoryGroupId", bl."CategoryId",
                   r."BudgetedAmount", r."Currency", r."RevisedAt", r."Note"
            FROM "BudgetLines" bl
            LEFT JOIN LATERAL (
                SELECT r2."BudgetedAmount", r2."Currency", r2."RevisedAt", r2."Note"
                FROM "BudgetLineRevisions" r2
                WHERE r2."BudgetLineId" = bl."Id"
                ORDER BY r2."RevisedAt" DESC
                LIMIT 1
            ) r ON true
            WHERE bl."PeriodId" = @PeriodId AND bl."DeletedAt" IS NULL
            ORDER BY bl."Name"
            """,
            new { query.PeriodId });

        var items = rows
            .Select(r => new BudgetLineResponse(
                r.Id,
                r.Name,
                ((LineType)r.LineType).ToString(),
                r.IsRecurring,
                r.CategoryGroupId,
                r.CategoryId,
                r.BudgetedAmount,
                r.Currency,
                r.RevisedAt.HasValue
                    ? new DateTimeOffset(r.RevisedAt.Value, TimeSpan.Zero)
                    : null,
                r.Note))
            .ToList();

        return Result<IReadOnlyList<BudgetLineResponse>>.Success(items);
    }

    private sealed record BudgetLineRow(
        Guid      Id,
        string    Name,
        int       LineType,
        bool      IsRecurring,
        Guid      CategoryGroupId,
        Guid?     CategoryId,
        decimal?  BudgetedAmount,
        string?   Currency,
        DateTime? RevisedAt,
        string?   Note);
}
