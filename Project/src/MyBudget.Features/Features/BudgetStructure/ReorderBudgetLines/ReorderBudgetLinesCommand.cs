using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ReorderBudgetLines;

// TODO PR2a: scope changed to BudgetId only — periodId removed (REQ-BL-05)
public sealed record ReorderBudgetLinesCommand(
    Guid   BudgetId,
    Guid[] OrderedIds
) : IRequest<Result<bool>>;
