using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class CategoryGroupEntityTests
{
    private static CategoryGroup BuildGroup() =>
        CategoryGroup.Create(Guid.NewGuid(), "Housing", 1);

    [Fact]
    public void Restore_ClearsDeletedAt()
    {
        var group = BuildGroup();
        group.SoftDelete();
        group.DeletedAt.ShouldNotBeNull();

        group.Restore();

        group.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Restore_RefreshesUpdatedAt()
    {
        var group = BuildGroup();
        group.SoftDelete();

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        group.Restore();

        group.UpdatedAt.ShouldNotBeNull();
        group.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }
}
