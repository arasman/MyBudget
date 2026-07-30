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

        var sql = query.IncludeDeleted
            ? """
              SELECT
                  "Id",
                  "CurrencyId",
                  "Alias",
                  "IsPositive",
                  "DisplayOrder",
                  "DeletedAt"
              FROM "BankAccounts"
              WHERE "BudgetId" = @BudgetId
              ORDER BY "DisplayOrder" ASC
              """
            : """
              SELECT
                  "Id",
                  "CurrencyId",
                  "Alias",
                  "IsPositive",
                  "DisplayOrder",
                  "DeletedAt"
              FROM "BankAccounts"
              WHERE "BudgetId"  = @BudgetId
                AND "DeletedAt" IS NULL
              ORDER BY "DisplayOrder" ASC
              """;

        var rows = await conn.QueryAsync<BankAccountRow>(sql, new { query.BudgetId });

        var result = rows
            .Select(r => new BankAccountDto(
                r.Id,
                r.CurrencyId,
                r.Alias,
                r.IsPositive,
                r.DisplayOrder,
                r.DeletedAt.HasValue ? new DateTimeOffset(r.DeletedAt.Value, TimeSpan.Zero) : null))
            .ToList();

        return Result<IReadOnlyList<BankAccountDto>>.Success(result);
    }

    private sealed record BankAccountRow(
        Guid      Id,
        Guid      CurrencyId,
        string    Alias,
        bool      IsPositive,
        int       DisplayOrder,
        DateTime? DeletedAt);
}
