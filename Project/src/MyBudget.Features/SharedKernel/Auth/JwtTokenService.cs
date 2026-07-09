using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MyBudget.Features.SharedKernel.Entities;

namespace MyBudget.Features.SharedKernel.Auth;

/// <summary>
/// Generates JWT access tokens and random refresh tokens.
/// Registered as Scoped in AddFeatures.
/// </summary>
public sealed class JwtTokenService
{
    private readonly JwtOptions _opts;

    public JwtTokenService(IOptions<JwtOptions> opts)
    {
        _opts = opts.Value;
    }

    /// <summary>
    /// Generates a signed JWT access token containing exactly the five required claims:
    /// sub (userId), email, jti, iat, exp. No roles or budget IDs.
    /// </summary>
    public string GenerateAccessToken(User user)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var now         = DateTime.UtcNow;

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject            = new ClaimsIdentity(claims),
            Issuer             = _opts.Issuer,
            Audience           = _opts.Audience,
            IssuedAt           = now,
            Expires            = now.AddMinutes(_opts.AccessTokenExpiryMinutes),
            SigningCredentials  = credentials,
        };

        var handler = new JwtSecurityTokenHandler();
        var token   = handler.CreateToken(descriptor);
        return handler.WriteToken(token);
    }

    /// <summary>
    /// Generates a cryptographically random 64-byte Base64Url-encoded refresh token.
    /// Two consecutive calls are guaranteed to return different values.
    /// </summary>
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}
