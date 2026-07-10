using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreatePeriod;

public sealed record CreatePeriodCommand(
    Guid     BudgetId,
    Guid     CycleId,
    string   Name,
    int      PeriodNumber,
    DateOnly StartDate,
    DateOnly EndDate
) : IRequest<Result<Guid>>;
