using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCategoryGroup;

public sealed record DeleteCategoryGroupCommand(
    Guid BudgetId,
    Guid GroupId
) : IRequest<Result<Guid>>;
