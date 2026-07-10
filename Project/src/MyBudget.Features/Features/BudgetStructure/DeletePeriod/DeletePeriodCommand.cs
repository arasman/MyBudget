using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeletePeriod;

public sealed record DeletePeriodCommand(
    Guid BudgetId,
    Guid CycleId,
    Guid PeriodId
) : IRequest<Result<Guid>>;
