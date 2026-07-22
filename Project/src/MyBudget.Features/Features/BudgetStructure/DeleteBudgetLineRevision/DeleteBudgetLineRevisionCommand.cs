using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.DeleteBudgetLineRevision;

public sealed record DeleteBudgetLineRevisionCommand(
    Guid BudgetId,
    Guid LineId,
    Guid RevisionId)
    : IRequest<Result<Guid>>;
