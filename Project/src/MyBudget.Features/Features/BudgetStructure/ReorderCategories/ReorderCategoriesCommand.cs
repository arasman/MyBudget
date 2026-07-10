using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ReorderCategories;

public sealed record ReorderCategoriesCommand(
    Guid       BudgetId,
    Guid       CategoryGroupId,
    List<Guid> OrderedIds
) : IRequest<Result<bool>>;
