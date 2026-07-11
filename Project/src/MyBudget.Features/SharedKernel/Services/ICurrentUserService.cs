namespace MyBudget.Features.SharedKernel.Services;

/// <summary>
/// Provides the authenticated user's identity for the current request.
/// Returns null when there is no HTTP context (e.g., background jobs, migration seeds).
/// </summary>
public interface ICurrentUserService
{
    Guid? UserId { get; }
}
