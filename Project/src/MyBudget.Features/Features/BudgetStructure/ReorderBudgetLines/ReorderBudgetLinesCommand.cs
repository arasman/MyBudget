using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ReorderBudgetLines;

public sealed record ReorderBudgetLinesCommand(
    Guid   BudgetId,
    Guid   PeriodId,
    Guid[] OrderedIds
) : IRequest<Result<bool>>;
