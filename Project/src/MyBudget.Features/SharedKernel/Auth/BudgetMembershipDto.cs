namespace MyBudget.Features.SharedKernel.Auth;

/// <summary>Read-only view of a user's budget membership. Used in CurrentUserResponse.</summary>
public sealed record BudgetMembershipDto(
    Guid   BudgetId,
    string BudgetName,
    string Role);
