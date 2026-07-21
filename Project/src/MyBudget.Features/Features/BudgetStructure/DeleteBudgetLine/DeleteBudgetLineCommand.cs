using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeleteBudgetLine;

public sealed record DeleteBudgetLineCommand(
    Guid BudgetId,
    Guid LineId
) : IRequest<Result<Guid>>;
