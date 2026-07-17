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

    // Two static queries avoid string interpolation for structural SQL differences.
    // Dapper parameterization covers values only (@BudgetId); column/clause changes require separate statements.
    // Two separate CycleRow record types are used because Dapper infers System.DateTime (not DateTimeOffset?)
    // for NULL literal columns — using separate records avoids the constructor type-mismatch at materialisation.
    private const string SqlActive = """
        SELECT c."Id", c."Name", c."StartDate", c."EndDate", c."IsActive",
               COUNT(p."Id") AS "PeriodCount",
               dc."Id"     AS "DefaultCurrencyId",
               dc."Code"   AS "DefaultCurrencyCode",
               dc."Symbol" AS "DefaultCurrencySymbol",
               c."AlternateCurrencyId",
               c."ExchangeRate",
               ac."Code"   AS "AlternateCurrencyCode",
               ac."Symbol" AS "AlternateCurrencySymbol"
        FROM "Cycles" c
        INNER JOIN "Currencies" dc ON dc."Id" = c."DefaultCurrencyId"
        LEFT  JOIN "Currencies" ac ON ac."Id" = c."AlternateCurrencyId"
        -- PeriodCount always reflects active-only periods (DeletedAt IS NULL), intentionally,
        -- to avoid surfacing deleted period counts to the user even when IncludeDeleted=true.
        LEFT  JOIN "Periods" p ON p."CycleId" = c."Id" AND p."DeletedAt" IS NULL
        WHERE c."BudgetId" = @BudgetId
          AND c."DeletedAt" IS NULL
        GROUP BY c."Id", c."Name", c."StartDate", c."EndDate", c."IsActive",
                 dc."Id", dc."Code", dc."Symbol",
                 c."AlternateCurrencyId", c."ExchangeRate",
                 ac."Code", ac."Symbol"
        ORDER BY c."StartDate"
        """;

    private const string SqlIncludeDeleted = """
        SELECT c."Id", c."Name", c."StartDate", c."EndDate", c."IsActive",
               COUNT(p."Id") AS "PeriodCount",
               dc."Id"     AS "DefaultCurrencyId",
               dc."Code"   AS "DefaultCurrencyCode",
               dc."Symbol" AS "DefaultCurrencySymbol",
               c."AlternateCurrencyId",
               c."ExchangeRate",
               ac."Code"   AS "AlternateCurrencyCode",
               ac."Symbol" AS "AlternateCurrencySymbol",
               c."DeletedAt"
        FROM "Cycles" c
        INNER JOIN "Currencies" dc ON dc."Id" = c."DefaultCurrencyId"
        LEFT  JOIN "Currencies" ac ON ac."Id" = c."AlternateCurrencyId"
        -- PeriodCount always reflects active-only periods (DeletedAt IS NULL), intentionally,
        -- to avoid surfacing deleted period counts to the user even when IncludeDeleted=true.
        LEFT  JOIN "Periods" p ON p."CycleId" = c."Id" AND p."DeletedAt" IS NULL
        WHERE c."BudgetId" = @BudgetId
        GROUP BY c."Id", c."Name", c."StartDate", c."EndDate", c."IsActive",
                 dc."Id", dc."Code", dc."Symbol",
                 c."AlternateCurrencyId", c."ExchangeRate",
                 ac."Code", ac."Symbol", c."DeletedAt"
        ORDER BY c."StartDate"
        """;

    public async ValueTask<Result<IReadOnlyList<CycleListItem>>> Handle(
        ListCyclesQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        if (query.IncludeDeleted)
        {
            var rows = await conn.QueryAsync<CycleRowDeleted>(SqlIncludeDeleted, new { BudgetId = query.BudgetId });
            return Result<IReadOnlyList<CycleListItem>>.Success(rows.Select(MapDeleted).ToList());
        }
        else
        {
            var rows = await conn.QueryAsync<CycleRow>(SqlActive, new { BudgetId = query.BudgetId });
            return Result<IReadOnlyList<CycleListItem>>.Success(rows.Select(MapActive).ToList());
        }
    }

    private static CycleListItem MapActive(CycleRow r)
    {
        CurrencyDto? alt = r.AlternateCurrencyCode is not null
            ? new CurrencyDto(r.AlternateCurrencyId!.Value, r.AlternateCurrencyCode, r.AlternateCurrencySymbol!)
            : null;
        return new CycleListItem(
            r.Id, r.Name, r.StartDate, r.EndDate, r.IsActive, (int)r.PeriodCount,
            new CurrencyDto(r.DefaultCurrencyId, r.DefaultCurrencyCode, r.DefaultCurrencySymbol),
            alt, r.AlternateCurrencyId, r.ExchangeRate, null);
    }

    private static CycleListItem MapDeleted(CycleRowDeleted r)
    {
        CurrencyDto? alt = r.AlternateCurrencyCode is not null
            ? new CurrencyDto(r.AlternateCurrencyId!.Value, r.AlternateCurrencyCode, r.AlternateCurrencySymbol!)
            : null;
        DateTimeOffset? deletedAt = r.DeletedAt.HasValue
            ? new DateTimeOffset(r.DeletedAt.Value, TimeSpan.Zero)
            : null;
        return new CycleListItem(
            r.Id, r.Name, r.StartDate, r.EndDate, r.IsActive, (int)r.PeriodCount,
            new CurrencyDto(r.DefaultCurrencyId, r.DefaultCurrencyCode, r.DefaultCurrencySymbol),
            alt, r.AlternateCurrencyId, r.ExchangeRate, deletedAt);
    }

    // Npgsql 10 maps PostgreSQL date as DateOnly and COUNT as Int64.
    private sealed record CycleRow(
        Guid             Id,
        string           Name,
        DateOnly         StartDate,
        DateOnly         EndDate,
        bool             IsActive,
        long             PeriodCount,
        Guid             DefaultCurrencyId,
        string           DefaultCurrencyCode,
        string           DefaultCurrencySymbol,
        Guid?            AlternateCurrencyId,
        decimal?         ExchangeRate,
        string?          AlternateCurrencyCode,
        string?          AlternateCurrencySymbol);

    // Extends CycleRow with DeletedAt for the IncludeDeleted=true branch.
    // Npgsql maps timestamptz → DateTime in this project's mode; MapDeleted converts to DateTimeOffset.
    private sealed record CycleRowDeleted(
        Guid      Id,
        string    Name,
        DateOnly  StartDate,
        DateOnly  EndDate,
        bool      IsActive,
        long      PeriodCount,
        Guid      DefaultCurrencyId,
        string    DefaultCurrencyCode,
        string    DefaultCurrencySymbol,
        Guid?     AlternateCurrencyId,
        decimal?  ExchangeRate,
        string?   AlternateCurrencyCode,
        string?   AlternateCurrencySymbol,
        DateTime? DeletedAt);
}
