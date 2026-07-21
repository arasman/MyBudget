using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.RestoreBudgetLine;

// TODO PR2a: route updated — periodId removed (REQ-RST-05)
public sealed record RestoreBudgetLineCommand(
    Guid BudgetId,
    Guid BudgetLineId,
    bool IncludeExecutionRecords
) : IRequest<Result<Guid>>;
