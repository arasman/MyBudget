namespace MyBudget.Features.SharedKernel.Auth;

/// <summary>Response body returned by login and register endpoints.</summary>
public sealed record LoginResponse(
    string      AccessToken,
    string      RefreshToken,
    int         ExpiresIn,
    UserProfile User);

/// <summary>User profile subset included in the login response.</summary>
public sealed record UserProfile(
    Guid   Id,
    string Email,
    string FirstName,
    string LastName,
    string PreferredLocale);
