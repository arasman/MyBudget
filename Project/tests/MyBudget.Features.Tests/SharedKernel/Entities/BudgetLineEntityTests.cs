using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class BudgetLineEntityTests
{
    [Fact]
    public void Create_AcceptsDisplayOrder()
    {
        var line = BudgetLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Rent", LineType.Expense, true,
            displayOrder: 3);

        line.DisplayOrder.ShouldBe(3);
    }

    [Fact]
    public void Create_DefaultDisplayOrder_IsZero()
    {
        var line = BudgetLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Rent", LineType.Expense, false);

        line.DisplayOrder.ShouldBe(0);
    }

    [Fact]
    public void SetDisplayOrder_UpdatesDisplayOrder()
    {
        var line = BudgetLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Rent", LineType.Expense, false);

        line.SetDisplayOrder(5);

        line.DisplayOrder.ShouldBe(5);
    }

    [Fact]
    public void Restore_ClearsDeletedAt()
    {
        var line = BudgetLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Rent", LineType.Expense, false);

        line.SoftDelete();
        line.DeletedAt.ShouldNotBeNull();

        line.Restore();

        line.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Restore_RefreshesUpdatedAt()
    {
        var line = BudgetLine.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null,
            "Rent", LineType.Expense, false);

        line.SoftDelete();

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        line.Restore();

        line.UpdatedAt.ShouldNotBeNull();
        line.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }
}
