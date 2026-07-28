using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.CurrentSituation.UpsertCutRecord;

public sealed record UpsertCutRecordCommand(
    Guid                              BudgetId,
    DateOnly                          CutDate,
    decimal                           ExchangeRate,
    string?                           ProjectionsJson,
    IReadOnlyList<UpsertCutBankAccountItem> Accounts
) : IRequest<Result<bool>>;

public sealed record UpsertCutBankAccountItem(
    Guid    BankAccountId,
    decimal Balance);
