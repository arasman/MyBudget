using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.CurrentSituation.ListCutDates;

public sealed class ListCutDatesHandler
    : IRequestHandler<ListCutDatesQuery, Result<IReadOnlyList<DateOnly>>>
{
    private readonly ConnectionFactory _factory;

    public ListCutDatesHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<IReadOnlyList<DateOnly>>> Handle(
        ListCutDatesQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        const string sql = """
            SELECT "CutDate"
            FROM "CutRecords"
            WHERE "BudgetId" = @BudgetId
            ORDER BY "CutDate" ASC
            """;

        var dates = await conn.QueryAsync<DateOnly>(sql, new { query.BudgetId });

        return Result<IReadOnlyList<DateOnly>>.Success(dates.ToList());
    }
}
