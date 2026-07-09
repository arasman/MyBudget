using MyBudget.Features.Features.Budgets.InviteUserToBudget;
using MyBudget.Features.SharedKernel.Entities;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Budgets.InviteUserToBudget;

public sealed class InviteUserToBudgetValidatorTests
{
    private readonly InviteUserToBudgetValidator _sut = new();

    private static InviteUserToBudgetCommand ValidCommand() =>
        new(Guid.NewGuid(), "invitee@example.com", BudgetRole.Operator, Guid.NewGuid());

    [Fact]
    public void ValidEmailAndRole_Passes()
    {
        _sut.Validate(ValidCommand()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Email_Empty_Fails()
    {
        _sut.Validate(ValidCommand() with { InviteeEmail = "" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Email_TooLong_Fails()
    {
        _sut.Validate(ValidCommand() with { InviteeEmail = new string('a', 250) + "@b.com" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Email_InvalidFormat_Fails()
    {
        _sut.Validate(ValidCommand() with { InviteeEmail = "not-an-email" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Role_Owner_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Role = BudgetRole.Owner });
        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.ErrorCode == "AUTH_CANNOT_INVITE_AS_OWNER");
    }

    [Theory]
    [InlineData(BudgetRole.Admin)]
    [InlineData(BudgetRole.Operator)]
    [InlineData(BudgetRole.ReadOnly)]
    public void Role_NonOwner_Passes(BudgetRole role)
    {
        _sut.Validate(ValidCommand() with { Role = role }).IsValid.ShouldBeTrue();
    }
}
