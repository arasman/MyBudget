using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class BankAccountEntityTests
{
    private static readonly Guid DefaultCurrencyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static BankAccount BuildAccount() =>
        BankAccount.Create(Guid.NewGuid(), DefaultCurrencyId, "Test Account", true, 1);

    [Fact]
    public void Restore_ClearsDeletedAt_And_RefreshesUpdatedAt()
    {
        var account = BuildAccount();
        account.SoftDelete();
        account.DeletedAt.ShouldNotBeNull();

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        account.Restore();

        account.DeletedAt.ShouldBeNull();
        account.UpdatedAt.ShouldNotBeNull();
        account.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }

    [Fact]
    public void Restore_OnAlreadyActiveAccount_DoesNotThrow_And_RefreshesUpdatedAt()
    {
        var account = BuildAccount();
        account.DeletedAt.ShouldBeNull();

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);

        // Should not throw
        var exception = Record.Exception(() => account.Restore());
        exception.ShouldBeNull();

        account.DeletedAt.ShouldBeNull();
        account.UpdatedAt.ShouldNotBeNull();
        account.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }

    [Fact]
    public void Restore_DoesNotModifyAlias_CurrencyId_IsPositive_Or_DisplayOrder()
    {
        var currencyId = DefaultCurrencyId;
        var account = BankAccount.Create(Guid.NewGuid(), currencyId, "My Alias", false, 3);
        account.SoftDelete();

        account.Restore();

        account.Alias.ShouldBe("My Alias");
        account.CurrencyId.ShouldBe(currencyId);
        account.IsPositive.ShouldBeFalse();
        account.DisplayOrder.ShouldBe(3);
    }
}
