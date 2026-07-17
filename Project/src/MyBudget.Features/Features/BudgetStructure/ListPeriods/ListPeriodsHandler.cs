using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListPeriods;

/// <summary>
/// Dapper read — verifies cycle belongs to budget, then returns Periods ordered by PeriodNumber.
/// Supports optional inclusion of soft-deleted periods via IncludeDeleted flag.
/// Returns 404 if cycle not found or belongs to a different budget.
/// </summary>
public sealed class ListPeriodsHandler
    : IRequestHandler<ListPeriodsQuery, Result<IReadOnlyList<PeriodListItem>>>
{
    private const string SqlCycleExists = """
        SELECT EXISTS (
            SELECT 1
            FROM "Cycles"
            WHERE "Id" = @CycleId
              AND "BudgetId" = @BudgetId
              AND "DeletedAt" IS NULL
        )
        """;

    private const string SqlActive = """
        SELECT "Id", "Name", "PeriodNumber", "StartDate", "EndDate", "IsClosed"
        FROM "Periods"
        WHERE "CycleId" = @CycleId
          AND "DeletedAt" IS NULL
        ORDER BY "PeriodNumber"
        """;

    private const string SqlIncludeDeleted = """
        SELECT "Id", "Name", "PeriodNumber", "StartDate", "EndDate", "IsClosed", "DeletedAt"
        FROM "Periods"
        WHERE "CycleId" = @CycleId
        ORDER BY "PeriodNumber"
        """;

    private readonly ConnectionFactory _factory;

    public ListPeriodsHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<IReadOnlyList<PeriodListItem>>> Handle(
        ListPeriodsQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        var cycleExists = await conn.ExecuteScalarAsync<bool>(
            SqlCycleExists,
            new { query.CycleId, query.BudgetId });

        if (!cycleExists)
            return Result<IReadOnlyList<PeriodListItem>>.Failure("CYCLE_NOT_FOUND");

        IReadOnlyList<PeriodListItem> items;

        if (query.IncludeDeleted)
        {
            var rows = await conn.QueryAsync<PeriodRowDeleted>(
                SqlIncludeDeleted,
                new { query.CycleId });

            items = rows
                .Select(r => new PeriodListItem(
                    r.Id,
                    r.Name,
                    r.PeriodNumber,
                    r.StartDate,
                    r.EndDate,
                    r.IsClosed,
                    r.DeletedAt.HasValue
                        ? new DateTimeOffset(r.DeletedAt.Value, TimeSpan.Zero)
                        : null))
                .ToList();
        }
        else
        {
            var rows = await conn.QueryAsync<PeriodRow>(
                SqlActive,
                new { query.CycleId });

            items = rows
                .Select(r => new PeriodListItem(
                    r.Id,
                    r.Name,
                    r.PeriodNumber,
                    r.StartDate,
                    r.EndDate,
                    r.IsClosed))
                .ToList();
        }

        return Result<IReadOnlyList<PeriodListItem>>.Success(items);
    }

    private sealed record PeriodRow(
        Guid     Id,
        string   Name,
        int      PeriodNumber,
        DateOnly StartDate,
        DateOnly EndDate,
        bool     IsClosed);

    private sealed record PeriodRowDeleted(
        Guid      Id,
        string    Name,
        int       PeriodNumber,
        DateOnly  StartDate,
        DateOnly  EndDate,
        bool      IsClosed,
        DateTime? DeletedAt);
}
