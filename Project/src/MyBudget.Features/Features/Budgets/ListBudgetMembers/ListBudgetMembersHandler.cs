using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.ListBudgetMembers;

/// <summary>
/// Dapper read — returns every membership for a budget with user profile fields.
/// WU1 scope only: no soft-delete filtering / includeDeleted param yet (WU2 additive extension).
/// </summary>
public sealed class ListBudgetMembersHandler
    : IRequestHandler<ListBudgetMembersQuery, Result<ListBudgetMembersResponse>>
{
    private readonly ConnectionFactory _factory;

    public ListBudgetMembersHandler(ConnectionFactory factory) => _factory = factory;

    private const string Sql = """
        SELECT bm."UserId", u."Email", u."FirstName", u."LastName", bm."Role", bm."JoinedAt"
        FROM "BudgetMemberships" bm
        JOIN "Users" u ON u."Id" = bm."UserId"
        WHERE bm."BudgetId" = @BudgetId
        ORDER BY bm."JoinedAt"
        """;

    public async ValueTask<Result<ListBudgetMembersResponse>> Handle(
        ListBudgetMembersQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();
        var rows = await conn.QueryAsync<MemberRow>(Sql, new { BudgetId = query.BudgetId });

        var members = rows.Select(r => new MemberListItem(
            r.UserId,
            r.Email,
            r.FirstName,
            r.LastName,
            ((BudgetRole)r.Role).ToApiString(),
            new DateTimeOffset(r.JoinedAt, TimeSpan.Zero))).ToList();

        return Result<ListBudgetMembersResponse>.Success(new ListBudgetMembersResponse(members));
    }

    // Npgsql maps timestamp with time zone to DateTime in this project's mode (see GetCurrentUserHandler).
    private sealed record MemberRow(
        Guid     UserId,
        string   Email,
        string   FirstName,
        string   LastName,
        int      Role,
        DateTime JoinedAt);
}
