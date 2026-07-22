using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLineRevision;

public sealed record UpdateBudgetLineRevisionCommand(
    Guid    BudgetId,
    Guid    LineId,
    Guid    RevisionId,
    decimal Amount,
    string? Note)
    : IRequest<Result<Unit>>;
