using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListCycles;

/// <summary>Dapper read — returns all non-deleted Cycles for a budget with period counts.</summary>
public sealed class ListCyclesHandler
    : IRequestHandler<ListCyclesQuery, Result<IReadOnlyList<CycleListItem>>>
{
    private readonly ConnectionFactory _factory;

    public ListCyclesHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<IReadOnlyList<CycleListItem>>> Handle(
        ListCyclesQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        var rows = await conn.QueryAsync<CycleRow>(
            """
            SELECT c."Id", c."Name", c."StartDate", c."EndDate", c."IsActive",
                   COUNT(p."Id") AS "PeriodCount"
            FROM "Cycles" c
            LEFT JOIN "Periods" p ON p."CycleId" = c."Id" AND p."DeletedAt" IS NULL
            WHERE c."BudgetId" = @BudgetId AND c."DeletedAt" IS NULL
            GROUP BY c."Id", c."Name", c."StartDate", c."EndDate", c."IsActive"
            ORDER BY c."StartDate"
            """,
            new { BudgetId = query.BudgetId });

        var items = rows
            .Select(r => new CycleListItem(
                r.Id,
                r.Name,
                r.StartDate,
                r.EndDate,
                r.IsActive,
                (int)r.PeriodCount))
            .ToList();

        return Result<IReadOnlyList<CycleListItem>>.Success(items);
    }

    // Npgsql 10 maps PostgreSQL date as DateOnly and COUNT as Int64.
    private sealed record CycleRow(
        Guid     Id,
        string   Name,
        DateOnly StartDate,
        DateOnly EndDate,
        bool     IsActive,
        long     PeriodCount);
}
