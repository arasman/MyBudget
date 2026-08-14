using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.ListBudgetMembers;

public sealed record ListBudgetMembersQuery(Guid BudgetId, bool IncludeDeleted = false)
    : IRequest<Result<ListBudgetMembersResponse>>;

public sealed record ListBudgetMembersResponse(IReadOnlyList<MemberListItem> Members);

public sealed record MemberListItem(
    Guid            UserId,
    string          Email,
    string          FirstName,
    string          LastName,
    string          Role,
    DateTimeOffset  JoinedAt,
    bool            IsDeleted,
    DateTimeOffset? DeletedAt);
