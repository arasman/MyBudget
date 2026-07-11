using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListCurrencies;

/// <summary>Dapper read — returns all currencies ordered by code. BudgetId is ignored (global catalog).</summary>
public sealed class ListCurrenciesHandler
    : IRequestHandler<ListCurrenciesQuery, Result<IReadOnlyList<CurrencyResponse>>>
{
    private readonly ConnectionFactory _factory;

    public ListCurrenciesHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<IReadOnlyList<CurrencyResponse>>> Handle(
        ListCurrenciesQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        var rows = await conn.QueryAsync<CurrencyResponse>(
            """
            SELECT "Id", "Code", "Name", "Symbol"
            FROM "Currencies"
            ORDER BY "Code"
            """);

        return Result<IReadOnlyList<CurrencyResponse>>.Success(rows.ToList());
    }
}
