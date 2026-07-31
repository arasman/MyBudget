using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Dashboard.GetLifetimeCutTotals;

public sealed record GetLifetimeCutTotalsQuery(
    Guid BudgetId
) : IRequest<Result<LifetimeCutTotalsResponse>>;

public sealed record LifetimeCutTotalsResponse(
    string                            ConversionBasis,
    IReadOnlyList<CutTotalsPointDto>  Points);

public sealed record CutTotalsPointDto(
    DateOnly CutDate,
    decimal  ExchangeRate,
    decimal  TotalPositive,         decimal TotalPositiveAlt,
    decimal  TotalNegative,         decimal TotalNegativeAlt,
    decimal  TotalDeudaEnCurso,     decimal TotalDeudaEnCursoAlt,
    decimal  TotalBudgeted,         decimal TotalBudgetedAlt,
    decimal  TotalRegistered,       decimal TotalRegisteredAlt,
    decimal  Remaining,             decimal RemainingAlt,
    decimal  TotalAvailable,        decimal TotalAvailableAlt,
    decimal  TotalNet,              decimal TotalNetAlt);
