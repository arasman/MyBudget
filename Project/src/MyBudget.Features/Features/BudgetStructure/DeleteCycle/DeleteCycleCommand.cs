using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeleteCycle;

public sealed record DeleteCycleCommand(
    Guid BudgetId,
    Guid CycleId
) : IRequest<Result<Guid>>;
