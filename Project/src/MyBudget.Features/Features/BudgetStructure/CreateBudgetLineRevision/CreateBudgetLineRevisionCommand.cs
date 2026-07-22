using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLineRevision;

public sealed record CreateBudgetLineRevisionCommand(
    Guid      BudgetId,
    Guid      LineId,
    DateOnly  ValidFrom,
    DateOnly? ValidTo,
    decimal   Amount,
    Guid?     CurrencyId)
    : IRequest<Result<Guid>>;
