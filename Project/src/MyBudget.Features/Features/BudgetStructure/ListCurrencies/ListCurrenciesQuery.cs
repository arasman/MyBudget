using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListCurrencies;

public sealed record ListCurrenciesQuery(Guid BudgetId)
    : IRequest<Result<IReadOnlyList<CurrencyResponse>>>;

public sealed record CurrencyResponse(Guid Id, string Code, string Name, string Symbol);
