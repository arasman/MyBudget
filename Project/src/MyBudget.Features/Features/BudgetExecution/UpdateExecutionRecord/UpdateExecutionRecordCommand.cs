using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetExecution.UpdateExecutionRecord;

public sealed record UpdateExecutionRecordCommand(
    Guid      BudgetId,
    Guid      PeriodId,
    Guid      BudgetLineId,
    Guid      ExecutionId,
    EntryType EntryType,
    decimal   Amount,
    string?   Note,
    Guid      CurrencyId,
    decimal?  ExchangeRate,
    decimal?  ExchangeRateTo,
    Guid?     AccountId,
    Guid?     PaymentMethodId
) : IRequest<Result<Guid>>;
