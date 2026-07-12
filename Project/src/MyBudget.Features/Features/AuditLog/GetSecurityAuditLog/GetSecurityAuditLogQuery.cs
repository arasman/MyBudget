using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.AuditLog.GetSecurityAuditLog;

public sealed record GetSecurityAuditLogQuery(
    Guid BudgetId,
    int  Page     = 1,
    int  PageSize = 20)
    : IRequest<Result<PagedResult<SecurityAuditLogItem>>>;

public sealed record SecurityAuditLogItem(
    Guid            Id,
    string          Event,
    Guid?           UserId,
    string?         Email,
    string?         IpAddress,
    string?         UserAgent,
    DateTimeOffset  Timestamp,
    string?         Details);

public sealed record PagedResult<T>(
    IEnumerable<T> Items,
    int            TotalCount,
    int            Page,
    int            PageSize);
