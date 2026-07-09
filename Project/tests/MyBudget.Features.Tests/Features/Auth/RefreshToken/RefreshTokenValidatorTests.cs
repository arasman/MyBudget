using MyBudget.Features.Features.Auth.RefreshToken;
using Shouldly;

namespace MyBudget.Features.Tests.Features.Auth.RefreshToken;

public sealed class RefreshTokenValidatorTests
{
    private readonly RefreshTokenValidator _sut = new();

    private static RefreshTokenCommand ValidCommand() =>
        new("some-refresh-token-value", Guid.NewGuid());

    [Fact]
    public void ValidPayload_Passes()
    {
        _sut.Validate(ValidCommand()).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void RefreshToken_Empty_Fails()
    {
        _sut.Validate(ValidCommand() with { RefreshToken = "" }).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void UserId_Empty_Fails()
    {
        _sut.Validate(ValidCommand() with { UserId = Guid.Empty }).IsValid.ShouldBeFalse();
    }
}
