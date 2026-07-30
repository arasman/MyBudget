using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BankAccounts.CreateBankAccount;

public sealed class CreateBankAccountHandler : IRequestHandler<CreateBankAccountCommand, Result<Guid>>
{
    private readonly AppDbContext _db;

    public CreateBankAccountHandler(AppDbContext db) => _db = db;

    public async ValueTask<Result<Guid>> Handle(CreateBankAccountCommand cmd, CancellationToken ct)
    {
        var budgetExists = await _db.Budgets
            .AnyAsync(b => b.Id == cmd.BudgetId, ct);

        if (!budgetExists)
            return Result<Guid>.Failure("BUDGET_NOT_FOUND");

        var currencyExists = await _db.Currencies
            .AnyAsync(c => c.Id == cmd.CurrencyId, ct);

        if (!currencyExists)
            return Result<Guid>.Failure("CURRENCY_NOT_FOUND");

        var account = BankAccount.Create(
            cmd.BudgetId,
            cmd.CurrencyId,
            cmd.Alias,
            cmd.IsPositive,
            cmd.DisplayOrder);

        _db.BankAccounts.Add(account);
        await _db.SaveChangesAsync(ct);

        return Result<Guid>.Success(account.Id);
    }
}
