using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.CurrentSituation.DeleteCutRecord;

public sealed record DeleteCutRecordCommand(
    Guid     BudgetId,
    DateOnly CutDate
) : IRequest<Result<bool>>;
