using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestorePeriod;

public sealed record RestorePeriodCommand(
    Guid BudgetId,
    Guid CycleId,
    Guid PeriodId,
    bool IncludeExecutionRecords
) : IRequest<Result<Guid>>;
