using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.DeleteExecutionRecord;

public sealed record DeleteExecutionRecordCommand(
    Guid BudgetId,
    Guid PeriodId,
    Guid BudgetLineId,
    Guid ExecutionId
) : IRequest<Result<Guid>>;
