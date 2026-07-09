using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace MyBudget.Features.SharedKernel.Auth.Authorization;

/// <summary>
/// Custom authorization middleware result handler that returns 404 when the
/// BudgetAuthorizationHandler signals that the budget resource does not exist,
/// instead of the default 403 Forbidden.
/// </summary>
public sealed class BudgetAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate        next,
        HttpContext            context,
        AuthorizationPolicy   policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Succeeded &&
            context.Items.TryGetValue("budget-not-found", out _))
        {
            context.Response.StatusCode  = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"BUDGET_NOT_FOUND\"}");
            return;
        }

        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
