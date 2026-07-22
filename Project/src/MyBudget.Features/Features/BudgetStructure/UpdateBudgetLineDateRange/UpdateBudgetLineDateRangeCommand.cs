using Mediator;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLineDateRange;

public sealed record UpdateBudgetLineDateRangeCommand(
    Guid      BudgetId,
    Guid      LineId,
    DateOnly  StartDate,
    DateOnly? EndDate)
    : IRequest<Result<Guid>>;
