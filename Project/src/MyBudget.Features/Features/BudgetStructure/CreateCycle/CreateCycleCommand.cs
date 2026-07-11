using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.CreateCycle;

public sealed record CreateCycleCommand(
    Guid     BudgetId,
    string   Name,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid     DefaultCurrencyId,
    Guid?    AlternateCurrencyId,
    decimal? ExchangeRate
) : IRequest<Result<Guid>>;
