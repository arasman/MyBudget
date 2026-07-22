using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListBudgetLineRevisions;

public sealed record ListBudgetLineRevisionsQuery(
    Guid BudgetId,
    Guid LineId)
    : IRequest<Result<IReadOnlyList<RevisionDto>>>;

public sealed record RevisionDto(
    Guid      Id,
    Guid      BudgetLineId,
    decimal   BudgetedAmount,
    Guid      CurrencyId,
    string?   CurrencyCode,
    string?   CurrencySymbol,
    DateOnly  ValidFrom,
    DateOnly? ValidTo,
    string?   Note);
