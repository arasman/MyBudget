using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;

namespace MyBudget.Features.SharedKernel.Auth.Authorization;

/// <summary>
/// Resolves the current user's BudgetRole from IMemoryCache (TTL 5 min) or Dapper fallback.
/// Roles are NEVER read from the JWT — always resolved from BudgetMemberships table.
/// </summary>
public sealed class BudgetAuthorizationHandler
    : AuthorizationHandler<BudgetRequirement>
{
    private readonly IMemoryCache     _cache;
    private readonly ConnectionFactory _factory;

    public BudgetAuthorizationHandler(IMemoryCache cache, ConnectionFactory factory)
    {
        _cache   = cache;
        _factory = factory;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        BudgetRequirement requirement)
    {
        // 1. Extract userId from JWT sub claim
        var userIdRaw = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdRaw, out var userId))
        {
            context.Fail();
            return;
        }

        // 2. Extract budgetId from route values
        var httpContext = context.Resource as HttpContext;
        var budgetIdRaw = httpContext?.Request.RouteValues["id"]?.ToString();
        if (!Guid.TryParse(budgetIdRaw, out var budgetId))
        {
            context.Fail();
            return;
        }

        // 3. Cache lookup
        var cacheKey = $"budget-membership:{userId}:{budgetId}";
        if (!_cache.TryGetValue(cacheKey, out BudgetRole? cachedRole))
        {
            // 4. Dapper fallback — JOIN Budgets to filter out soft-deleted budgets
            using var conn  = _factory.CreateConnection();
            var role = await conn.QuerySingleOrDefaultAsync<int?>(
                """
                SELECT bm."Role"
                FROM "BudgetMemberships" bm
                JOIN "Budgets" b ON b."Id" = bm."BudgetId"
                WHERE bm."UserId" = @UserId AND bm."BudgetId" = @BudgetId
                  AND b."IsDeleted" = false
                  AND bm."IsDeleted" = false
                LIMIT 1
                """,
                new { UserId = userId, BudgetId = budgetId });

            if (!role.HasValue)
            {
                // Distinguish "budget not found or deleted" from "user not a member" so the
                // middleware result handler can return 404 instead of 403.
                var budgetExists = await conn.ExecuteScalarAsync<bool>(
                    """SELECT COUNT(1) > 0 FROM "Budgets" WHERE "Id" = @BudgetId""",
                    new { BudgetId = budgetId });

                if (!budgetExists && httpContext is not null)
                    httpContext.Items["budget-not-found"] = true;

                // If budget exists but is soft-deleted, also set budget-not-found flag
                if (budgetExists && httpContext is not null)
                {
                    var isDeleted = await conn.ExecuteScalarAsync<bool>(
                        """SELECT "IsDeleted" FROM "Budgets" WHERE "Id" = @BudgetId LIMIT 1""",
                        new { BudgetId = budgetId });

                    if (isDeleted)
                        httpContext.Items["budget-not-found"] = true;
                }

                context.Fail();
                return;
            }

            cachedRole = (BudgetRole)role.Value;

            _cache.Set(cacheKey, cachedRole, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
            });
        }

        // 5. Role comparison — int values from enum enable >= check
        if ((int)cachedRole! >= (int)requirement.MinimumRole)
            context.Succeed(requirement);
        else
            context.Fail();
    }
}
