using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;

public sealed record CreateBudgetLineCommand(
    Guid      BudgetId,
    Guid      CategoryGroupId,
    Guid?     CategoryId,
    string    Name,
    LineType  LineType,
    DateOnly  StartDate,
    DateOnly? EndDate,
    decimal   InitialAmount,
    Guid?     CurrencyId,
    string?   Description = null
) : IRequest<Result<Guid>>;
