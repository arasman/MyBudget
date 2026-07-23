using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListBudgetLines;

public sealed record ListBudgetLinesQuery(
    Guid BudgetId,
    bool IncludeDeleted = false)
    : IRequest<Result<IReadOnlyList<BudgetLineResponse>>>;

public sealed record BudgetLineResponse(
    Guid      Id,
    Guid      BudgetId,
    Guid      CategoryGroupId,
    Guid?     CategoryId,
    string    Name,
    string    LineType,
    int       DisplayOrder,
    DateOnly  StartDate,
    DateOnly? EndDate,
    decimal?  BudgetedAmount,
    Guid?     CurrencyId,
    string?   CurrencyCode,
    string?   CurrencySymbol,
    string?   Description,
    DateTimeOffset? DeletedAt = null);
