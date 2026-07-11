using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateBudgetLine;

public sealed record CreateBudgetLineCommand(
    Guid     BudgetId,
    Guid     PeriodId,
    Guid     CategoryGroupId,
    Guid?    CategoryId,
    string   Name,
    LineType LineType,
    bool     IsRecurring,
    decimal  BudgetedAmount,
    Guid?    CurrencyId
) : IRequest<Result<Guid>>;
