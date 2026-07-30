using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BankAccounts.UpdateBankAccount;

public sealed class UpdateBankAccountHandler : IRequestHandler<UpdateBankAccountCommand, Result<bool>>
{
    private readonly AppDbContext _db;

    public UpdateBankAccountHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<bool>> Handle(UpdateBankAccountCommand cmd, CancellationToken ct)
    {
        // Load active account (global query filter excludes soft-deleted)
        var account = await _db.BankAccounts
            .FirstOrDefaultAsync(
                a => a.Id == cmd.AccountId && a.BudgetId == cmd.BudgetId,
                ct);

        if (account is null)
            return Result<bool>.Failure("ACCOUNT_NOT_FOUND");

        account.Update(cmd.Alias, cmd.IsPositive, cmd.DisplayOrder);
        await _db.SaveChangesAsync(ct);

        return Result<bool>.Success(true);
    }
}
