using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using MyBudget.Features.SharedKernel.Auth;
using MyBudget.Features.SharedKernel.Entities;
using NSubstitute;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Auth;

public sealed class JwtTokenServiceTests
{
    private static JwtTokenService CreateService(int expiryMinutes = 15)
    {
        var opts = Substitute.For<IOptions<JwtOptions>>();
        opts.Value.Returns(new JwtOptions
        {
            Key                    = "test-secret-key-that-is-at-least-32-chars!!",
            Issuer                 = "TestIssuer",
            Audience               = "TestAudience",
            AccessTokenExpiryMinutes = expiryMinutes,
        });
        return new JwtTokenService(opts);
    }

    private static User CreateUser() =>
        User.Create("test@example.com", "hashedpw", "Test", "User");

    [Fact]
    public void GenerateAccessToken_ContainsExactlyFiveRequiredClaims()
    {
        var svc   = CreateService();
        var user  = CreateUser();
        var token = svc.GenerateAccessToken(user);

        var handler  = new JwtSecurityTokenHandler();
        var jwt      = handler.ReadJwtToken(token);
        var claimMap = jwt.Claims.ToDictionary(c => c.Type, c => c.Value);

        // sub, email, jti, iat, exp — required by spec
        claimMap.ContainsKey(JwtRegisteredClaimNames.Sub).ShouldBeTrue();
        claimMap.ContainsKey(JwtRegisteredClaimNames.Email).ShouldBeTrue();
        claimMap.ContainsKey(JwtRegisteredClaimNames.Jti).ShouldBeTrue();
        jwt.IssuedAt.ShouldNotBe(default);
        jwt.ValidTo.ShouldNotBe(default);

        // sub must equal userId
        claimMap[JwtRegisteredClaimNames.Sub].ShouldBe(user.Id.ToString());
        claimMap[JwtRegisteredClaimNames.Email].ShouldBe(user.Email);

        // Must NOT contain roles or budget IDs
        claimMap.ContainsKey(ClaimTypes.Role).ShouldBeFalse();
    }

    [Fact]
    public void GenerateAccessToken_IsSignedWithHmacSha256()
    {
        var svc   = CreateService();
        var token = svc.GenerateAccessToken(CreateUser());

        var handler = new JwtSecurityTokenHandler();
        var jwt     = handler.ReadJwtToken(token);

        jwt.Header.Alg.ShouldBe("HS256");
    }

    [Fact]
    public void GenerateAccessToken_ExpiryIsApproximatelyNowPlusConfiguredMinutes()
    {
        var svc   = CreateService(expiryMinutes: 15);
        var token = svc.GenerateAccessToken(CreateUser());

        var handler = new JwtSecurityTokenHandler();
        var jwt     = handler.ReadJwtToken(token);

        var expected = DateTime.UtcNow.AddMinutes(15);
        jwt.ValidTo.ShouldBeInRange(expected.AddSeconds(-5), expected.AddSeconds(5));
    }

    [Fact]
    public void GenerateRefreshToken_IsBase64UrlDecodableTo64Bytes()
    {
        var svc   = CreateService();
        var token = svc.GenerateRefreshToken();

        token.ShouldNotBeNullOrWhiteSpace();

        // Base64Url → Base64 for decoding
        var base64 = token
            .Replace('-', '+')
            .Replace('_', '/')
            .PadRight(token.Length + (4 - token.Length % 4) % 4, '=');

        var bytes = Convert.FromBase64String(base64);
        bytes.Length.ShouldBe(64);
    }

    [Fact]
    public void GenerateRefreshToken_TwoConsecutiveCallsReturnDifferentValues()
    {
        var svc = CreateService();
        var t1  = svc.GenerateRefreshToken();
        var t2  = svc.GenerateRefreshToken();
        t1.ShouldNotBe(t2);
    }
}
