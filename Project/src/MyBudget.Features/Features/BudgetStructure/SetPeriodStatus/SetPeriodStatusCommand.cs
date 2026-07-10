using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.SetPeriodStatus;

public sealed record SetPeriodStatusCommand(
    Guid BudgetId,
    Guid CycleId,
    Guid PeriodId,
    bool IsClosed
) : IRequest<Result<Guid>>;
