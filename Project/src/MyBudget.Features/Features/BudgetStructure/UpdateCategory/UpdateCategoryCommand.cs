using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCategory;

public sealed record UpdateCategoryCommand(
    Guid   BudgetId,
    Guid   CategoryGroupId,
    Guid   CategoryId,
    string Name,
    int    DisplayOrder
) : IRequest<Result<Guid>>;
