using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class BudgetDomainTests
{
    private static Budget BuildBudget() =>
        Budget.Create("Test Budget", Guid.NewGuid());

    // --- Rename ---

    [Fact]
    public void Rename_UpdatesName()
    {
        var budget = BuildBudget();
        budget.Rename("New Name");
        budget.Name.ShouldBe("New Name");
    }

    [Fact]
    public void Rename_TrimsWhitespace()
    {
        var budget = BuildBudget();
        budget.Rename("  Trimmed  ");
        budget.Name.ShouldBe("Trimmed");
    }

    [Fact]
    public void Rename_SetsUpdatedAt()
    {
        var budget = BuildBudget();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        budget.Rename("Updated");
        budget.UpdatedAt.ShouldNotBeNull();
        budget.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }

    // --- SoftDelete ---

    [Fact]
    public void SoftDelete_SetsIsDeletedTrue()
    {
        var budget = BuildBudget();
        budget.SoftDelete();
        budget.IsDeleted.ShouldBeTrue();
    }

    [Fact]
    public void SoftDelete_SetsDeletedAt()
    {
        var budget = BuildBudget();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        budget.SoftDelete();
        budget.DeletedAt.ShouldNotBeNull();
        budget.DeletedAt!.Value.ShouldBeGreaterThan(before);
    }

    [Fact]
    public void SoftDelete_SetsUpdatedAt()
    {
        var budget = BuildBudget();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        budget.SoftDelete();
        budget.UpdatedAt.ShouldNotBeNull();
        budget.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }

    // --- Restore ---

    [Fact]
    public void Restore_ClearsIsDeleted()
    {
        var budget = BuildBudget();
        budget.SoftDelete();
        budget.Restore();
        budget.IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public void Restore_ClearsDeletedAt()
    {
        var budget = BuildBudget();
        budget.SoftDelete();
        budget.Restore();
        budget.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Restore_SetsUpdatedAt()
    {
        var budget = BuildBudget();
        budget.SoftDelete();
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        budget.Restore();
        budget.UpdatedAt.ShouldNotBeNull();
        budget.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }

    // --- Create defaults ---

    [Fact]
    public void Create_SetsIsDeletedFalse()
    {
        var budget = BuildBudget();
        budget.IsDeleted.ShouldBeFalse();
    }

    [Fact]
    public void Create_SetsDeletedAtNull()
    {
        var budget = BuildBudget();
        budget.DeletedAt.ShouldBeNull();
    }
}
