using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateCycle;

public sealed record UpdateCycleCommand(
    Guid     BudgetId,
    Guid     CycleId,
    string   Name,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid     DefaultCurrencyId,
    Guid?    AlternateCurrencyId,
    decimal? ExchangeRate
) : IRequest<Result<Guid>>;
