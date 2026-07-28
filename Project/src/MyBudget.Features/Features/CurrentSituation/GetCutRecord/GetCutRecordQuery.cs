using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.CurrentSituation.GetCutRecord;

public sealed record GetCutRecordQuery(
    Guid     BudgetId,
    DateOnly CutDate
) : IRequest<Result<GetCutRecordResponse>>;

// ── Response DTOs ─────────────────────────────────────────────────────────

public sealed record GetCutRecordResponse(
    bool                            IsDraft,
    Guid?                           CutRecordId,
    DateOnly                        CutDate,
    decimal                         ExchangeRate,
    string?                         ProjectionsJson,
    BudgetExecutionSummaryDto       ExecutionSummary,
    IReadOnlyList<CutBankAccountDto> Accounts,
    CutTotalsDto                    Totals);

public sealed record BudgetExecutionSummaryDto(
    decimal TotalBudgeted,
    decimal TotalRegistered,
    decimal Remaining);

public sealed record CutBankAccountDto(
    Guid    BankAccountId,
    string  Alias,
    Guid    CurrencyId,
    bool    IsPositive,
    int     DisplayOrder,
    decimal Balance,
    decimal BalanceInPrimary);

public sealed record CutTotalsDto(
    decimal TotalPositive,
    decimal TotalNegative,
    decimal TotalDeudaEnCurso,
    decimal TotalPositiveAlt,
    decimal TotalNegativeAlt,
    decimal TotalDeudaEnCursoAlt);
