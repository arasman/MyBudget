using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ReorderCategoryGroups;

public sealed record ReorderCategoryGroupsCommand(
    Guid       BudgetId,
    List<Guid> OrderedIds
) : IRequest<Result<bool>>;
