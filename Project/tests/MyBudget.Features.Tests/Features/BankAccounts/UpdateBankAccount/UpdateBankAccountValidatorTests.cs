using MyBudget.Features.Features.BankAccounts.UpdateBankAccount;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BankAccounts.UpdateBankAccount;

/// <summary>
/// BA-3: UpdateBankAccount does NOT include CurrencyId (immutable after creation).
/// </summary>
public sealed class UpdateBankAccountValidatorTests : IDisposable
{
    private readonly MyBudget.Features.SharedKernel.Persistence.AppDbContext _db;
    private readonly UpdateBankAccountValidator _sut;

    public UpdateBankAccountValidatorTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new UpdateBankAccountValidator(_db);
    }

    public void Dispose() => _db.Dispose();

    private static UpdateBankAccountCommand ValidCommand() =>
        new(
            BudgetId:     Guid.NewGuid(),
            AccountId:    Guid.NewGuid(),
            Alias:        "Cuenta Principal",
            IsPositive:   true,
            DisplayOrder: 2);

    [Fact]
    public void Command_Has_No_CurrencyId_Property()
    {
        // CurrencyId MUST NOT exist on the update command (spec BA-3)
        var props = typeof(UpdateBankAccountCommand).GetProperties();
        props.ShouldNotContain(p => p.Name == "CurrencyId");
    }

    [Fact]
    public async Task Alias_Empty_Rejected()
    {
        var result = await _sut.ValidateAsync(ValidCommand() with { Alias = "" });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "FIELD_REQUIRED");
    }

    [Fact]
    public async Task Alias_101_Chars_Rejected()
    {
        var longAlias = new string('X', 101);
        var result    = await _sut.ValidateAsync(ValidCommand() with { Alias = longAlias });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "ALIAS_TOO_LONG");
    }

    [Fact]
    public async Task DisplayOrder_Negative_Rejected()
    {
        var result = await _sut.ValidateAsync(ValidCommand() with { DisplayOrder = -1 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "DISPLAY_ORDER_INVALID");
    }

    [Fact]
    public async Task Valid_Payload_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task AliasDuplicate_AnotherActiveAccount_Rejected()
    {
        var budgetId   = await DbTestHelpers.SeedBudgetAsync(_db);
        var accountAId = Guid.NewGuid();
        var accountB   = BankAccount.Create(budgetId, CurrencySeeds.GtqId, "Savings", true, 2);
        _db.BankAccounts.Add(accountB);
        await _db.SaveChangesAsync();

        var cmd    = new UpdateBankAccountCommand(budgetId, accountAId, "Savings", true, 1);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "ALIAS_DUPLICATE");
    }

    [Fact]
    public async Task AliasDuplicate_SoftDeletedAccount_Rejected()
    {
        var budgetId   = await DbTestHelpers.SeedBudgetAsync(_db);
        var accountAId = Guid.NewGuid();

        var deleted = BankAccount.Create(budgetId, CurrencySeeds.GtqId, "Archived", true, 2);
        deleted.SoftDelete();
        _db.BankAccounts.Add(deleted);
        await _db.SaveChangesAsync();

        var cmd    = new UpdateBankAccountCommand(budgetId, accountAId, "Archived", true, 1);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "ALIAS_DUPLICATE");
    }

    [Fact]
    public async Task OwnAlias_SelfExclusion_Accepted()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        // Seed the account and capture its auto-generated Id
        var account = BankAccount.Create(budgetId, CurrencySeeds.GtqId, "Checking", true, 1);
        _db.BankAccounts.Add(account);
        await _db.SaveChangesAsync();
        var accountId = account.Id;

        // Updating own alias should pass (self-exclusion)
        var cmd    = new UpdateBankAccountCommand(budgetId, accountId, "Checking", true, 1);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.ShouldBeTrue();
    }
}
