using Dapper;
using Mediator;
using MyBudget.Features.Features.BudgetStructure.GetCycleDetail;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListCycles;

/// <summary>Dapper read — returns all non-deleted Cycles for a budget with period counts and default currency.</summary>
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
                   COUNT(p."Id") AS "PeriodCount",
                   dc."Code"   AS "DefaultCurrencyCode",
                   dc."Symbol" AS "DefaultCurrencySymbol",
                   c."AlternateCurrencyId",
                   c."ExchangeRate",
                   ac."Code"   AS "AlternateCurrencyCode",
                   ac."Symbol" AS "AlternateCurrencySymbol"
            FROM "Cycles" c
            INNER JOIN "Currencies" dc ON dc."Id" = c."DefaultCurrencyId"
            LEFT  JOIN "Currencies" ac ON ac."Id" = c."AlternateCurrencyId"
            LEFT  JOIN "Periods" p ON p."CycleId" = c."Id" AND p."DeletedAt" IS NULL
            WHERE c."BudgetId" = @BudgetId AND c."DeletedAt" IS NULL
            GROUP BY c."Id", c."Name", c."StartDate", c."EndDate", c."IsActive",
                     dc."Code", dc."Symbol",
                     c."AlternateCurrencyId", c."ExchangeRate",
                     ac."Code", ac."Symbol"
            ORDER BY c."StartDate"
            """,
            new { BudgetId = query.BudgetId });

        var items = rows
            .Select(r =>
            {
                CurrencyDto? alternateCurrency = r.AlternateCurrencyCode is not null
                    ? new CurrencyDto(r.AlternateCurrencyCode, r.AlternateCurrencySymbol!)
                    : null;

                return new CycleListItem(
                    r.Id,
                    r.Name,
                    r.StartDate,
                    r.EndDate,
                    r.IsActive,
                    (int)r.PeriodCount,
                    new CurrencyDto(r.DefaultCurrencyCode, r.DefaultCurrencySymbol),
                    alternateCurrency,
                    r.AlternateCurrencyId,
                    r.ExchangeRate);
            })
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
        long     PeriodCount,
        string   DefaultCurrencyCode,
        string   DefaultCurrencySymbol,
        Guid?    AlternateCurrencyId,
        decimal? ExchangeRate,
        string?  AlternateCurrencyCode,
        string?  AlternateCurrencySymbol);
}
