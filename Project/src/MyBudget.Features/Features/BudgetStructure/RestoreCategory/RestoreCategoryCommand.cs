using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCategory;

public sealed record RestoreCategoryCommand(
    Guid BudgetId,
    Guid CategoryGroupId,
    Guid CategoryId,
    bool IncludeExecutionRecords
) : IRequest<Result<Guid>>;
