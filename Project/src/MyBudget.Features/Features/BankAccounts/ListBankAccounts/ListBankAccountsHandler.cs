using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BankAccounts.ListBankAccounts;

public sealed class ListBankAccountsHandler
    : IRequestHandler<ListBankAccountsQuery, Result<IReadOnlyList<BankAccountDto>>>
{
    private readonly ConnectionFactory _factory;

    public ListBankAccountsHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<IReadOnlyList<BankAccountDto>>> Handle(
        ListBankAccountsQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        const string sql = """
            SELECT
                "Id",
                "CurrencyId",
                "Alias",
                "IsPositive",
                "DisplayOrder"
            FROM "BankAccounts"
            WHERE "BudgetId"  = @BudgetId
              AND "DeletedAt" IS NULL
            ORDER BY "DisplayOrder" ASC
            """;

        var rows = await conn.QueryAsync<BankAccountDto>(sql, new { query.BudgetId });

        return Result<IReadOnlyList<BankAccountDto>>.Success(rows.ToList());
    }
}
