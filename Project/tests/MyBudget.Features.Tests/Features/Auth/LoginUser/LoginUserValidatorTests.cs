using MyBudget.Features.Features.Auth.LoginUser;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Auth.LoginUser;

public sealed class LoginUserValidatorTests
{
    private readonly LoginUserValidator _sut = new();

    private static LoginUserCommand ValidCommand() =>
        new("user@example.com", "anypassword");

    [Fact]
    public void ValidPayload_Passes()
    {
        _sut.Validate(ValidCommand()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Email_Empty_Fails()
    {
        _sut.Validate(ValidCommand() with { Email = "" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Email_InvalidFormat_Fails()
    {
        _sut.Validate(ValidCommand() with { Email = "notvalid" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Password_Empty_Fails()
    {
        _sut.Validate(ValidCommand() with { Password = "" }).IsValid.ShouldBeFalse();
    }
}
