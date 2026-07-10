using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListCategoryGroups;

public sealed record ListCategoryGroupsQuery(Guid BudgetId)
    : IRequest<Result<IReadOnlyList<CategoryGroupResponse>>>;

public sealed record CategoryGroupResponse(
    Guid                          Id,
    string                        Name,
    int                           DisplayOrder,
    IReadOnlyList<CategoryItem>   Categories);

public sealed record CategoryItem(
    Guid   Id,
    string Name,
    int    DisplayOrder);
