using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.GetCycleDetail;

/// <summary>Dapper read — returns Cycle + nested Periods. Returns 404 if not found.</summary>
public sealed class GetCycleDetailHandler
    : IRequestHandler<GetCycleDetailQuery, Result<CycleDetailResponse>>
{
    private readonly ConnectionFactory _factory;

    public GetCycleDetailHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<CycleDetailResponse>> Handle(
        GetCycleDetailQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        var cycleRow = await conn.QuerySingleOrDefaultAsync<CycleRow>(
            """
            SELECT c."Id", c."Name", c."StartDate", c."EndDate", c."IsActive"
            FROM "Cycles" c
            WHERE c."Id" = @CycleId AND c."BudgetId" = @BudgetId AND c."DeletedAt" IS NULL
            """,
            new { query.CycleId, query.BudgetId });

        if (cycleRow is null)
            return Result<CycleDetailResponse>.Failure("CYCLE_NOT_FOUND");

        var periodRows = await conn.QueryAsync<PeriodRow>(
            """
            SELECT p."Id", p."Name", p."PeriodNumber", p."StartDate", p."EndDate", p."IsClosed"
            FROM "Periods" p
            WHERE p."CycleId" = @CycleId AND p."DeletedAt" IS NULL
            ORDER BY p."PeriodNumber"
            """,
            new { query.CycleId });

        var periods = periodRows
            .Select(r => new PeriodSummary(
                r.Id,
                r.Name,
                r.PeriodNumber,
                r.StartDate,
                r.EndDate,
                r.IsClosed))
            .ToList();

        var response = new CycleDetailResponse(
            cycleRow.Id,
            cycleRow.Name,
            cycleRow.StartDate,
            cycleRow.EndDate,
            cycleRow.IsActive,
            periods);

        return Result<CycleDetailResponse>.Success(response);
    }

    // Npgsql 10 maps PostgreSQL date as DateOnly directly.
    private sealed record CycleRow(
        Guid     Id,
        string   Name,
        DateOnly StartDate,
        DateOnly EndDate,
        bool     IsActive);

    private sealed record PeriodRow(
        Guid     Id,
        string   Name,
        int      PeriodNumber,
        DateOnly StartDate,
        DateOnly EndDate,
        bool     IsClosed);
}
