using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;

// TODO PR2a: full command rewrite — remove PeriodId/IsRecurring/BudgetedAmount, add ValidFrom/ValidTo for revision split
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
