using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.CurrentSituation.ListCutDates;

public sealed record ListCutDatesQuery(Guid BudgetId) : IRequest<Result<IReadOnlyList<DateOnly>>>;
