using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.AuditLog.GetSecurityAuditLog;

/// <summary>
/// Dapper read — returns paginated SecurityAuditLog entries scoped to users
/// who are members of the given budget via JOIN on BudgetMemberships.
/// </summary>
public sealed class GetSecurityAuditLogHandler
    : IRequestHandler<GetSecurityAuditLogQuery, Result<PagedResult<SecurityAuditLogItem>>>
{
    private readonly ConnectionFactory _factory;

    public GetSecurityAuditLogHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<PagedResult<SecurityAuditLogItem>>> Handle(
        GetSecurityAuditLogQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        var offset = (query.Page - 1) * query.PageSize;

        const string dataQuery =
            """
            SELECT sal."Id", sal."Event", sal."UserId", sal."Email",
                   sal."IpAddress", sal."UserAgent", sal."Timestamp", sal."Details"
            FROM "SecurityAuditLogs" sal
            INNER JOIN "BudgetMemberships" bm ON bm."UserId" = sal."UserId"
            WHERE bm."BudgetId" = @budgetId
            ORDER BY sal."Timestamp" DESC
            LIMIT @pageSize OFFSET @offset
            """;

        const string countQuery =
            """
            SELECT COUNT(*)
            FROM "SecurityAuditLogs" sal
            INNER JOIN "BudgetMemberships" bm ON bm."UserId" = sal."UserId"
            WHERE bm."BudgetId" = @budgetId
            """;

        var parameters = new
        {
            budgetId = query.BudgetId,
            pageSize = query.PageSize,
            offset,
        };

        var rows       = await conn.QueryAsync<SecurityAuditLogRow>(dataQuery, parameters);
        var totalCount = await conn.ExecuteScalarAsync<long>(countQuery, parameters);

        var items = rows.Select(r => new SecurityAuditLogItem(
            r.Id,
            r.Event,
            r.UserId,
            r.Email,
            r.IpAddress,
            r.UserAgent,
            new DateTimeOffset(r.Timestamp, TimeSpan.Zero),
            r.Details));

        return Result<PagedResult<SecurityAuditLogItem>>.Success(
            new PagedResult<SecurityAuditLogItem>(items, (int)totalCount, query.Page, query.PageSize));
    }

    // Npgsql maps timestamptz as DateTime in Dapper — convert to DateTimeOffset in projection.
    private sealed record SecurityAuditLogRow(
        Guid      Id,
        string    Event,
        Guid?     UserId,
        string?   Email,
        string?   IpAddress,
        string?   UserAgent,
        DateTime  Timestamp,
        string?   Details);
}
