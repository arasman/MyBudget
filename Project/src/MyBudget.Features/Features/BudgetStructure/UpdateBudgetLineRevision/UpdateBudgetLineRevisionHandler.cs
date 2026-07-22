using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.UpdateBudgetLineRevision;

public sealed class UpdateBudgetLineRevisionHandler
    : IRequestHandler<UpdateBudgetLineRevisionCommand, Result<Unit>>
{
    private readonly AppDbContext _db;

    public UpdateBudgetLineRevisionHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Unit>> Handle(
        UpdateBudgetLineRevisionCommand cmd, CancellationToken ct)
    {
        var revision = await _db.BudgetLineRevisions
            .FirstOrDefaultAsync(
                r => r.Id == cmd.RevisionId
                  && r.BudgetLineId == cmd.LineId
                  && r.BudgetId == cmd.BudgetId,
                ct);

        if (revision is null)
            return Result<Unit>.Failure("REVISION_NOT_FOUND");

        if (cmd.Amount < 0)
            return Result<Unit>.Failure("REVISION_AMOUNT_INVALID");

        revision.UpdateRevision(cmd.Amount, cmd.Note);

        await _db.SaveChangesAsync(ct);

        return Result<Unit>.Success(Unit.Value);
    }
}
