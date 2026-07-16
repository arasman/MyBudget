using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.ListExecutionRecords;

public sealed record ListExecutionRecordsQuery(
    Guid BudgetId,
    Guid PeriodId,
    Guid BudgetLineId,
    bool IncludeDeleted = false
) : IRequest<Result<IReadOnlyList<ExecutionRecordDto>>>;

public sealed record ExecutionRecordDto(
    Guid      Id,
    int       EntryType,
    decimal   Amount,
    Guid      CurrencyId,
    decimal?  ExchangeRate,
    decimal?  ExchangeRateTo,
    Guid?     AccountId,
    Guid?     PaymentMethodId,
    string?   Note,
    DateTimeOffset  CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? DeletedAt,
    DateOnly?       OperationDate = null);
