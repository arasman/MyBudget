using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestoreCycle;

public sealed record RestoreCycleCommand(
    Guid BudgetId,
    Guid CycleId,
    bool IncludeExecutionRecords
) : IRequest<Result<Guid>>;
