using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateCategory;

public sealed record CreateCategoryCommand(
    Guid   BudgetId,
    Guid   CategoryGroupId,
    string Name,
    int    DisplayOrder
) : IRequest<Result<Guid>>;
