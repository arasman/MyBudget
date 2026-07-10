using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateCategoryGroup;

public sealed record CreateCategoryGroupCommand(
    Guid   BudgetId,
    string Name,
    int    DisplayOrder
) : IRequest<Result<Guid>>;
