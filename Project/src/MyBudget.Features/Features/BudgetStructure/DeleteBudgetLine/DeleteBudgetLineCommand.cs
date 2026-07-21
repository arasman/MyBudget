using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeleteBudgetLine;

// TODO PR2a: route updated to remove periodId (REQ-BL-04)
public sealed record DeleteBudgetLineCommand(
    Guid BudgetId,
    Guid LineId
) : IRequest<Result<Guid>>;
