using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdatePeriod;

public sealed record UpdatePeriodCommand(
    Guid     BudgetId,
    Guid     CycleId,
    Guid     PeriodId,
    string   Name,
    int      PeriodNumber,
    DateOnly StartDate,
    DateOnly EndDate
) : IRequest<Result<Guid>>;
