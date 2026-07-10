using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCategoryGroup;

public sealed record UpdateCategoryGroupCommand(
    Guid   BudgetId,
    Guid   GroupId,
    string Name,
    int    DisplayOrder
) : IRequest<Result<Guid>>;
