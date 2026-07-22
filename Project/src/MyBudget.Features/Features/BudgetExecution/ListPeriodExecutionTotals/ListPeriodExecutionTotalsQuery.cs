using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.ListPeriodExecutionTotals;

public sealed record ListPeriodExecutionTotalsQuery(
    Guid BudgetId,
    Guid PeriodId
) : IRequest<Result<PeriodExecutionTotalsResponse>>;

public sealed record PeriodExecutionTotalsResponse(
    IReadOnlyList<LineTotalDto>     LineTotals,
    IReadOnlyList<CategoryTotalDto> CategoryTotals);

public sealed record LineTotalDto(
    Guid    BudgetLineId,
    string  BudgetLineName,
    decimal BudgetedAmount,
    decimal TotalExpenses,
    decimal TotalCreditNotes,
    decimal TotalDebitNotes,
    decimal NetTotal);

public sealed record CategoryTotalDto(
    Guid    CategoryGroupId,
    string  CategoryGroupName,
    Guid?   CategoryId,
    string? CategoryName,
    decimal TotalExpenses,
    decimal TotalCreditNotes,
    decimal TotalDebitNotes,
    decimal NetTotal);
