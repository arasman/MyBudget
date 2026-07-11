using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestoreBudgetLine;

public sealed record RestoreBudgetLineCommand(
    Guid BudgetId,
    Guid PeriodId,
    Guid BudgetLineId,
    bool IncludeExecutionRecords
) : IRequest<Result<Guid>>;
