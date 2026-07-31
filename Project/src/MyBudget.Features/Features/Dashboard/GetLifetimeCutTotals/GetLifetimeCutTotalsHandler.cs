using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Dashboard.GetLifetimeCutTotals;

/// <summary>
/// Dapper read — every persisted CutRecord's 16 totals for a budget, ordered by CutDate
/// ascending, across all cycles/periods. Read-only, no write path. DASH-1.
/// Values are frozen at cut time (CutRecord.ExchangeRate) — conversionBasis is always
/// "cut-frozen" (DASH-9 basis labeling).
/// </summary>
public sealed class GetLifetimeCutTotalsHandler
    : IRequestHandler<GetLifetimeCutTotalsQuery, Result<LifetimeCutTotalsResponse>>
{
    private const string ConversionBasis = "cut-frozen";

    private readonly ConnectionFactory _factory;

    public GetLifetimeCutTotalsHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<LifetimeCutTotalsResponse>> Handle(
        GetLifetimeCutTotalsQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // DASH-1: full lifetime series, no period grouping — every CutRecord for the
        // budget, ordered by CutDate ascending. CutRecord has no soft-delete column.
        const string sql = """
            SELECT
                cr."CutDate"              AS "CutDate",
                cr."ExchangeRate"         AS "ExchangeRate",
                cr."TotalPositive"        AS "TotalPositive",
                cr."TotalPositiveAlt"     AS "TotalPositiveAlt",
                cr."TotalNegative"        AS "TotalNegative",
                cr."TotalNegativeAlt"     AS "TotalNegativeAlt",
                cr."TotalDeudaEnCurso"    AS "TotalDeudaEnCurso",
                cr."TotalDeudaEnCursoAlt" AS "TotalDeudaEnCursoAlt",
                cr."TotalBudgeted"        AS "TotalBudgeted",
                cr."TotalBudgetedAlt"     AS "TotalBudgetedAlt",
                cr."TotalRegistered"      AS "TotalRegistered",
                cr."TotalRegisteredAlt"   AS "TotalRegisteredAlt",
                cr."Remaining"            AS "Remaining",
                cr."RemainingAlt"         AS "RemainingAlt",
                cr."TotalAvailable"       AS "TotalAvailable",
                cr."TotalAvailableAlt"    AS "TotalAvailableAlt",
                cr."TotalNet"             AS "TotalNet",
                cr."TotalNetAlt"          AS "TotalNetAlt"
            FROM "CutRecords" cr
            WHERE cr."BudgetId" = @BudgetId
            ORDER BY cr."CutDate" ASC
            """;

        var points = (await conn.QueryAsync<CutTotalsPointDto>(sql, new { query.BudgetId })).ToList();

        return Result<LifetimeCutTotalsResponse>.Success(
            new LifetimeCutTotalsResponse(ConversionBasis, points));
    }
}
