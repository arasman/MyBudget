using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.RestoreExecutionRecord;

public sealed record RestoreExecutionRecordCommand(
    Guid BudgetId,
    Guid PeriodId,
    Guid BudgetLineId,
    Guid ExecutionId
) : IRequest<Result<Guid>>;
