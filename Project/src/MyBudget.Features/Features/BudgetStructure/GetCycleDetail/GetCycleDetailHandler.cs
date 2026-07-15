using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.GetCycleDetail;

/// <summary>Dapper read — returns Cycle + nested Periods with currency info. Returns 404 if not found.</summary>
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
            SELECT c."Id", c."Name", c."StartDate", c."EndDate", c."IsActive",
                   c."ExchangeRate",
                   dc."Id"     AS "DefaultCurrencyId",
                   dc."Code"   AS "DefaultCurrencyCode",
                   dc."Symbol" AS "DefaultCurrencySymbol",
                   ac."Id"     AS "AlternateCurrencyId",
                   ac."Code"   AS "AlternateCurrencyCode",
                   ac."Symbol" AS "AlternateCurrencySymbol"
            FROM "Cycles" c
            INNER JOIN "Currencies" dc ON dc."Id" = c."DefaultCurrencyId"
            LEFT  JOIN "Currencies" ac ON ac."Id" = c."AlternateCurrencyId"
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

        CurrencyDto? alternateCurrency = cycleRow.AlternateCurrencyCode is not null
            ? new CurrencyDto(cycleRow.AlternateCurrencyId!.Value, cycleRow.AlternateCurrencyCode, cycleRow.AlternateCurrencySymbol!)
            : null;

        var response = new CycleDetailResponse(
            cycleRow.Id,
            cycleRow.Name,
            cycleRow.StartDate,
            cycleRow.EndDate,
            cycleRow.IsActive,
            new CurrencyDto(cycleRow.DefaultCurrencyId, cycleRow.DefaultCurrencyCode, cycleRow.DefaultCurrencySymbol),
            alternateCurrency,
            cycleRow.ExchangeRate,
            periods);

        return Result<CycleDetailResponse>.Success(response);
    }

    private sealed record CycleRow(
        Guid     Id,
        string   Name,
        DateOnly StartDate,
        DateOnly EndDate,
        bool     IsActive,
        decimal? ExchangeRate,
        Guid     DefaultCurrencyId,
        string   DefaultCurrencyCode,
        string   DefaultCurrencySymbol,
        Guid?    AlternateCurrencyId,
        string?  AlternateCurrencyCode,
        string?  AlternateCurrencySymbol);

    private sealed record PeriodRow(
        Guid     Id,
        string   Name,
        int      PeriodNumber,
        DateOnly StartDate,
        DateOnly EndDate,
        bool     IsClosed);
}
