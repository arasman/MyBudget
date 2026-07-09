namespace MyBudget.Features.SharedKernel.Auth;

/// <summary>Full user profile returned by GET /api/auth/me.</summary>
public sealed record CurrentUserResponse(
    Guid                             Id,
    string                           Email,
    string                           FirstName,
    string                           LastName,
    string                           PreferredLocale,
    DateTime?                        LastLoginAt,
    DateTimeOffset                   CreatedAt,
    IReadOnlyList<BudgetMembershipDto> Memberships);
