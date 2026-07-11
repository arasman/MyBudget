using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLine;

public sealed record UpdateBudgetLineCommand(
    Guid     BudgetId,
    Guid     PeriodId,
    Guid     LineId,
    Guid     CategoryGroupId,
    Guid?    CategoryId,
    string   Name,
    LineType LineType,
    bool     IsRecurring,
    decimal  BudgetedAmount,
    Guid?    CurrencyId
) : IRequest<Result<Guid>>;
