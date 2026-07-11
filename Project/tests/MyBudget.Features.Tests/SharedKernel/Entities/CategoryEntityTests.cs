using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class CategoryEntityTests
{
    private static Category BuildCategory() =>
        Category.Create(Guid.NewGuid(), "Rent", 1);

    [Fact]
    public void Restore_ClearsDeletedAt()
    {
        var category = BuildCategory();
        category.SoftDelete();
        category.DeletedAt.ShouldNotBeNull();

        category.Restore();

        category.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Restore_RefreshesUpdatedAt()
    {
        var category = BuildCategory();
        category.SoftDelete();

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        category.Restore();

        category.UpdatedAt.ShouldNotBeNull();
        category.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }
}
