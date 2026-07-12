using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.AuditLog.GetAuditLog;

/// <summary>Dapper read — returns paginated AuditLog entries for a budget with optional filters.</summary>
public sealed class GetAuditLogHandler
    : IRequestHandler<GetAuditLogQuery, Result<PagedResult<AuditLogItem>>>
{
    private readonly ConnectionFactory _factory;

    public GetAuditLogHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<PagedResult<AuditLogItem>>> Handle(
        GetAuditLogQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        var offset = (query.Page - 1) * query.PageSize;

        const string dataQuery =
            """
            SELECT "Id", "EntityName", "EntityId", "Action", "UserId",
                   "Timestamp", "BeforeJson", "AfterJson", "BudgetId"
            FROM "AuditLogs"
            WHERE "BudgetId" = @budgetId
              AND (@entityName IS NULL OR "EntityName" = @entityName)
              AND (@action IS NULL OR "Action" = @action)
              AND (@from IS NULL OR "Timestamp" >= @from)
              AND (@to IS NULL OR "Timestamp" <= @to)
            ORDER BY "Timestamp" DESC
            LIMIT @pageSize OFFSET @offset
            """;

        const string countQuery =
            """
            SELECT COUNT(*)
            FROM "AuditLogs"
            WHERE "BudgetId" = @budgetId
              AND (@entityName IS NULL OR "EntityName" = @entityName)
              AND (@action IS NULL OR "Action" = @action)
              AND (@from IS NULL OR "Timestamp" >= @from)
              AND (@to IS NULL OR "Timestamp" <= @to)
            """;

        var parameters = new
        {
            budgetId   = query.BudgetId,
            entityName = query.EntityName,
            action     = query.Action,
            from       = query.From,
            to         = query.To,
            pageSize   = query.PageSize,
            offset,
        };

        var rows = await conn.QueryAsync<AuditLogRow>(dataQuery, parameters);
        var totalCount = await conn.ExecuteScalarAsync<long>(countQuery, parameters);

        var items = rows.Select(r => new AuditLogItem(
            r.Id,
            r.EntityName,
            r.EntityId,
            r.Action,
            r.UserId,
            new DateTimeOffset(r.Timestamp, TimeSpan.Zero),
            r.BeforeJson,
            r.AfterJson,
            r.BudgetId));

        return Result<PagedResult<AuditLogItem>>.Success(
            new PagedResult<AuditLogItem>(items, (int)totalCount, query.Page, query.PageSize));
    }

    // Npgsql maps timestamptz as DateTime in Dapper — convert to DateTimeOffset in projection.
    private sealed record AuditLogRow(
        Guid      Id,
        string    EntityName,
        Guid      EntityId,
        string    Action,
        Guid?     UserId,
        DateTime  Timestamp,
        string?   BeforeJson,
        string?   AfterJson,
        Guid?     BudgetId);
}
