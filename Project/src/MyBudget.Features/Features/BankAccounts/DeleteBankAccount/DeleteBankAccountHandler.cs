using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BankAccounts.DeleteBankAccount;

public sealed class DeleteBankAccountHandler : IRequestHandler<DeleteBankAccountCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public DeleteBankAccountHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<bool>> Handle(DeleteBankAccountCommand cmd, CancellationToken ct)
    {
        // Bypass soft-delete query filter to find already-deleted accounts too
        var account = await _db.BankAccounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                a => a.Id == cmd.AccountId && a.BudgetId == cmd.BudgetId,
                ct);

        if (account is null)
            return Result<bool>.Failure("ACCOUNT_NOT_FOUND");

        account.SoftDelete();
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
