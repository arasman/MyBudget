namespace MyBudget.Features.SharedKernel.Entities;

/// <summary>
/// Single conversion point between <see cref="BudgetRole"/> and the API's kebab-case string
/// convention (owner, admin, operator, read-only). Prevents each call site from re-deriving its
/// own emit/parse rule and drifting out of sync (see design decision 3).
/// </summary>
public static class BudgetRoleStrings
{
    /// <summary>Emits the API contract string for a role: hyphenated for ReadOnly, lowercase otherwise.</summary>
    public static string ToApiString(this BudgetRole role) =>
        role == BudgetRole.ReadOnly ? "read-only" : role.ToString().ToLowerInvariant();

    /// <summary>Parses the API contract string back into a <see cref="BudgetRole"/>. Case-insensitive.</summary>
    public static bool TryParse(string? value, out BudgetRole role)
    {
        (role, var ok) = value?.ToLowerInvariant() switch
        {
            "owner"     => (BudgetRole.Owner,    true),
            "admin"     => (BudgetRole.Admin,    true),
            "operator"  => (BudgetRole.Operator, true),
            "read-only" => (BudgetRole.ReadOnly, true),
            _           => (default,             false),
        };
        return ok;
    }
}
