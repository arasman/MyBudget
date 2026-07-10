using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateCycle;

public sealed record CreateCycleCommand(
    Guid     BudgetId,
    string   Name,
    DateOnly StartDate,
    DateOnly EndDate
) : IRequest<Result<Guid>>;
