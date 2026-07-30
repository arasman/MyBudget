using MyBudget.Features.Features.BankAccounts.CreateBankAccount;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.Tests.Helpers;
using Shouldly;

namespace MyBudget.Features.Tests.Features.BankAccounts.CreateBankAccount;

public sealed class CreateBankAccountValidatorTests : IDisposable
{
    private readonly MyBudget.Features.SharedKernel.Persistence.AppDbContext _db;
    private readonly CreateBankAccountValidator _sut;

    public CreateBankAccountValidatorTests()
    {
        _db  = DbTestHelpers.CreateSqliteContext();
        _sut = new CreateBankAccountValidator(_db);
    }

    public void Dispose() => _db.Dispose();

    private static CreateBankAccountCommand ValidCommand() =>
        new(
            BudgetId:     Guid.NewGuid(),
            CurrencyId:   CurrencySeeds.GtqId,
            Alias:        "Caja GTQ",
            IsPositive:   true,
            DisplayOrder: 1);

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
        var longAlias = new string('A', 101);
        var result    = await _sut.ValidateAsync(ValidCommand() with { Alias = longAlias });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "ALIAS_TOO_LONG");
    }

    [Fact]
    public async Task Alias_100_Chars_Passes()
    {
        var alias  = new string('A', 100);
        var result = await _sut.ValidateAsync(ValidCommand() with { Alias = alias });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task DisplayOrder_Negative_Rejected()
    {
        var result = await _sut.ValidateAsync(ValidCommand() with { DisplayOrder = -1 });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "DISPLAY_ORDER_INVALID");
    }

    [Fact]
    public async Task DisplayOrder_Zero_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand() with { DisplayOrder = 0 });
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task CurrencyId_Empty_Rejected()
    {
        var result = await _sut.ValidateAsync(ValidCommand() with { CurrencyId = Guid.Empty });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "FIELD_REQUIRED");
    }

    [Fact]
    public async Task Valid_Payload_Passes()
    {
        var result = await _sut.ValidateAsync(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task AliasDuplicate_ActiveAccount_Rejected()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var existing = BankAccount.Create(budgetId, CurrencySeeds.GtqId, "Savings", true, 1);
        _db.BankAccounts.Add(existing);
        await _db.SaveChangesAsync();

        var cmd    = new CreateBankAccountCommand(budgetId, CurrencySeeds.GtqId, "Savings", true, 2);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "ALIAS_DUPLICATE");
    }

    [Fact]
    public async Task AliasDuplicate_SoftDeletedAccount_Rejected()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var existing = BankAccount.Create(budgetId, CurrencySeeds.GtqId, "OldChecking", true, 1);
        existing.SoftDelete();
        _db.BankAccounts.Add(existing);
        await _db.SaveChangesAsync();

        var cmd    = new CreateBankAccountCommand(budgetId, CurrencySeeds.GtqId, "OldChecking", true, 2);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "ALIAS_DUPLICATE");
    }

    [Fact]
    public async Task UniqueAlias_InSameBudget_Accepted()
    {
        var budgetId = await DbTestHelpers.SeedBudgetAsync(_db);

        var existing = BankAccount.Create(budgetId, CurrencySeeds.GtqId, "Savings", true, 1);
        _db.BankAccounts.Add(existing);
        await _db.SaveChangesAsync();

        var cmd    = new CreateBankAccountCommand(budgetId, CurrencySeeds.GtqId, "Checking", true, 2);
        var result = await _sut.ValidateAsync(cmd);

        result.IsValid.ShouldBeTrue();
    }
}
