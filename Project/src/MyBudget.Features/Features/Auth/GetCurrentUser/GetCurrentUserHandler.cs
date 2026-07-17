using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Auth.GetCurrentUser;

/// <summary>Dapper-only read — no EF Core. Joins Users and BudgetMemberships in one query.</summary>
public sealed class GetCurrentUserHandler
    : IRequestHandler<GetCurrentUserQuery, Result<CurrentUserResponse>>
{
    private readonly ConnectionFactory _factory;

    public GetCurrentUserHandler(ConnectionFactory factory)
    {
        _factory = factory;
    }

    public async ValueTask<Result<CurrentUserResponse>> Handle(
        GetCurrentUserQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // Two-query approach: user profile + memberships (simple, avoids cartesian product)
        var userRow = await conn.QuerySingleOrDefaultAsync<UserRow>(
            """
            SELECT "Id", "Email", "FirstName", "LastName", "PreferredLocale", "LastLoginAt", "CreatedAt"
            FROM "Users"
            WHERE "Id" = @UserId
            """,
            new { UserId = query.UserId });

        if (userRow is null)
            return Result<CurrentUserResponse>.Failure("USER_NOT_FOUND");

        var memberships = (await conn.QueryAsync<MembershipRow>(
            """
            SELECT bm."BudgetId", b."Name" AS BudgetName, bm."Role", b."IsDeleted"
            FROM "BudgetMemberships" bm
            JOIN "Budgets" b ON b."Id" = bm."BudgetId"
            WHERE bm."UserId" = @UserId
            ORDER BY bm."JoinedAt"
            """,
            new { UserId = query.UserId })).ToList();

        var membershipDtos = memberships
            .Select(m => new BudgetMembershipDto(
                m.BudgetId,
                m.BudgetName,
                ((BudgetRole)m.Role).ToString().ToLowerInvariant(),
                m.IsDeleted))
            .ToList();

        var response = new CurrentUserResponse(
            Id:              userRow.Id,
            Email:           userRow.Email,
            FirstName:       userRow.FirstName,
            LastName:        userRow.LastName,
            PreferredLocale: userRow.PreferredLocale,
            LastLoginAt:     userRow.LastLoginAt,
            CreatedAt:       new DateTimeOffset(userRow.CreatedAt, TimeSpan.Zero),
            Memberships:     membershipDtos);

        return Result<CurrentUserResponse>.Success(response);
    }

    private sealed record UserRow(
        Guid      Id,
        string    Email,
        string    FirstName,
        string    LastName,
        string    PreferredLocale,
        DateTime? LastLoginAt,
        DateTime  CreatedAt);  // Dapper reads timestamp with time zone as DateTime (UTC) via Npgsql 8+

    private sealed record MembershipRow(Guid BudgetId, string BudgetName, int Role, bool IsDeleted);
}
