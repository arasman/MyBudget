using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCategory;

public sealed record DeleteCategoryCommand(
    Guid BudgetId,
    Guid CategoryGroupId,
    Guid CategoryId
) : IRequest<Result<Guid>>;
