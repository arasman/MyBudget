using MyBudget.Features.Features.Auth.RegisterUser;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Auth.RegisterUser;

public sealed class RegisterUserValidatorTests
{
    private readonly RegisterUserValidator _sut = new();

    private static RegisterUserCommand ValidCommand() =>
        new("user@example.com", "Password1", "John", "Doe", "en");

    [Fact]
    public void ValidPayload_Passes()
    {
        var result = _sut.Validate(ValidCommand());
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Email_Missing_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Email = "" });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Email_InvalidFormat_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Email = "not-an-email" });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Password_TooShort_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Password = "Abc1" });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Password_TooLong_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Password = "A1" + new string('a', 71) });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Password_NoUppercase_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Password = "password1" });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Password_NoDigit_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { Password = "Password" });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void FirstName_TooLong_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { FirstName = new string('a', 101) });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void LastName_Missing_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { LastName = "" });
        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void PreferredLocale_Unsupported_Fails()
    {
        var result = _sut.Validate(ValidCommand() with { PreferredLocale = "fr" });
        result.IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    public void PreferredLocale_Supported_Passes(string locale)
    {
        var result = _sut.Validate(ValidCommand() with { PreferredLocale = locale });
        result.IsValid.ShouldBeTrue();
    }
}
