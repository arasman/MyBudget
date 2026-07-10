using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.SetActiveCycle;

public sealed record SetActiveCycleCommand(
    Guid BudgetId,
    Guid CycleId
) : IRequest<Result<Guid>>;
