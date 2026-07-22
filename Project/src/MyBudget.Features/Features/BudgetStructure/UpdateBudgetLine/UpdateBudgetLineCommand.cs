using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;

public sealed record UpdateBudgetLineCommand(
    Guid      BudgetId,
    Guid      LineId,
    Guid      CategoryGroupId,
    Guid?     CategoryId,
    string    Name,
    LineType  LineType,
    // Revision split fields — null means metadata-only update
    DateOnly? ValidFrom,
    DateOnly? ValidTo,
    decimal?  BudgetedAmount,
    Guid?     CurrencyId
) : IRequest<Result<Guid>>;
