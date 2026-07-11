using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class PeriodEntityTests
{
    private static Period BuildPeriod() =>
        Period.Create(
            Guid.NewGuid(),
            "January",
            1,
            new DateOnly(2025, 1, 1),
            new DateOnly(2025, 1, 31));

    [Fact]
    public void Restore_ClearsDeletedAt()
    {
        var period = BuildPeriod();
        period.SoftDelete();
        period.DeletedAt.ShouldNotBeNull();

        period.Restore();

        period.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Restore_RefreshesUpdatedAt()
    {
        var period = BuildPeriod();
        period.SoftDelete();

        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        period.Restore();

        period.UpdatedAt.ShouldNotBeNull();
        period.UpdatedAt!.Value.ShouldBeGreaterThan(before);
    }
}
