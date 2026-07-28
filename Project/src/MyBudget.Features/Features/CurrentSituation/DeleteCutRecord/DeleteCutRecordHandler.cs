using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.CurrentSituation.DeleteCutRecord;

public sealed class DeleteCutRecordHandler : IRequestHandler<DeleteCutRecordCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DeleteCutRecordHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<bool>> Handle(DeleteCutRecordCommand cmd, CancellationToken ct)
    {
        var cutRecord = await _db.CutRecords
            .FirstOrDefaultAsync(
                cr => cr.BudgetId == cmd.BudgetId && cr.CutDate == cmd.CutDate,
                ct);

        if (cutRecord is null)
            return Result<bool>.Failure("CUT_RECORD_NOT_FOUND");

        _db.CutRecords.Remove(cutRecord);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
