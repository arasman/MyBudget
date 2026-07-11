using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCategoryGroup;

public sealed record RestoreCategoryGroupCommand(
    Guid BudgetId,
    Guid CategoryGroupId,
    bool IncludeExecutionRecords
) : IRequest<Result<Guid>>;
