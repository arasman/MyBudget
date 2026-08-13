using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Entities;

public sealed class BudgetRoleStringsTests
{
    [Fact]
    public void ToApiString_ReadOnly_ReturnsHyphenated()
    {
        BudgetRole.ReadOnly.ToApiString().ShouldBe("read-only");
    }

    [Theory]
    [InlineData(BudgetRole.Owner, "owner")]
    [InlineData(BudgetRole.Admin, "admin")]
    [InlineData(BudgetRole.Operator, "operator")]
    public void ToApiString_NonReadOnlyRoles_ReturnsLowercase(BudgetRole role, string expected)
    {
        role.ToApiString().ShouldBe(expected);
    }

    [Theory]
    [InlineData(BudgetRole.Owner)]
    [InlineData(BudgetRole.Admin)]
    [InlineData(BudgetRole.Operator)]
    [InlineData(BudgetRole.ReadOnly)]
    public void ToApiString_TryParse_RoundTrips(BudgetRole role)
    {
        var apiString = role.ToApiString();

        BudgetRoleStrings.TryParse(apiString, out var parsed).ShouldBeTrue();
        parsed.ShouldBe(role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("unknown")]
    [InlineData("read only")]
    public void TryParse_UnknownString_ReturnsFalse(string input)
    {
        BudgetRoleStrings.TryParse(input, out _).ShouldBeFalse();
    }
}
