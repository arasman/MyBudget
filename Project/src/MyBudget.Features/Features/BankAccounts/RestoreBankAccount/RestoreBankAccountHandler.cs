using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BankAccounts.RestoreBankAccount;

public sealed class RestoreBankAccountHandler : IRequestHandler<RestoreBankAccountCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public RestoreBankAccountHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(RestoreBankAccountCommand cmd, CancellationToken ct)
    {
        var account = await _db.BankAccounts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                a => a.Id == cmd.AccountId && a.BudgetId == cmd.BudgetId && a.DeletedAt != null,
                ct);

        if (account is null)
            return Result<Guid>.Failure("BANK_ACCOUNT_NOT_FOUND");

        account.Restore();

        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(account.Id);
    }
}
