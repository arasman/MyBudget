using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.AuditLog.GetAuditLog;

public sealed record GetAuditLogQuery(
    Guid             BudgetId,
    int              Page       = 1,
    int              PageSize   = 20,
    string?          EntityName = null,
    string?          Action     = null,
    DateTimeOffset?  From       = null,
    DateTimeOffset?  To         = null)
    : IRequest<Result<PagedResult<AuditLogItem>>>;

public sealed record AuditLogItem(
    Guid            Id,
    string          EntityName,
    Guid            EntityId,
    string          Action,
    Guid?           UserId,
    DateTimeOffset  Timestamp,
    string?         BeforeJson,
    string?         AfterJson,
    Guid?           BudgetId);

public sealed record PagedResult<T>(
    IEnumerable<T> Items,
    int            TotalCount,
    int            Page,
    int            PageSize);
