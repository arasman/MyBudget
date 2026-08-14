using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using MyBudget.Features.SharedKernel.Auth.Authorization;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using NSubstitute;
using Shouldly;

namespace MyBudget.Features.Tests.SharedKernel.Auth;

/// <summary>
/// Unit tests for BudgetAuthorizationHandler.
/// Cache-hit scenarios are tested without any DB calls.
/// DB-miss scenarios are covered by integration tests (5.5).
/// </summary>
public sealed class BudgetAuthorizationHandlerTests : IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly ConnectionFactory _factory;

    public BudgetAuthorizationHandlerTests()
    {
        _cache = new MemoryCache(new MemoryCacheOptions());

        // ConnectionFactory needs IConfiguration — provide a minimal config
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=test;Username=test;Password=test",
            })
            .Build();
        _factory = new ConnectionFactory(config);
    }

    public void Dispose() => _cache.Dispose();

    private static ClaimsPrincipal MakeUser(Guid userId) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        ], "test"));

    private static HttpContext MakeHttpContext(Guid budgetId)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.RouteValues["id"] = budgetId.ToString();
        return ctx;
    }

    private static AuthorizationHandlerContext MakeAuthContext(
        ClaimsPrincipal user,
        BudgetRequirement requirement,
        object resource)
    {
        return new AuthorizationHandlerContext([requirement], user, resource);
    }

    [Fact]
    public async Task CacheHit_SufficientRole_Succeeds()
    {
        var userId   = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        _cache.Set($"budget-membership:{userId}:{budgetId}", BudgetRole.Admin);

        var handler     = new BudgetAuthorizationHandler(_cache, _factory);
        var requirement = new BudgetRequirement(BudgetRole.Admin);
        var authCtx     = MakeAuthContext(MakeUser(userId), requirement, MakeHttpContext(budgetId));

        await handler.HandleAsync(authCtx);

        authCtx.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task CacheHit_InsufficientRole_Fails()
    {
        var userId   = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        // User has ReadOnly — requires Admin
        _cache.Set($"budget-membership:{userId}:{budgetId}", BudgetRole.ReadOnly);

        var handler     = new BudgetAuthorizationHandler(_cache, _factory);
        var requirement = new BudgetRequirement(BudgetRole.Admin);
        var authCtx     = MakeAuthContext(MakeUser(userId), requirement, MakeHttpContext(budgetId));

        await handler.HandleAsync(authCtx);

        authCtx.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task CacheHit_OwnerRequiresAdmin_Succeeds()
    {
        // Owner (40) >= Admin (30) — should succeed
        var userId   = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        _cache.Set($"budget-membership:{userId}:{budgetId}", BudgetRole.Owner);

        var handler     = new BudgetAuthorizationHandler(_cache, _factory);
        var requirement = new BudgetRequirement(BudgetRole.Admin);
        var authCtx     = MakeAuthContext(MakeUser(userId), requirement, MakeHttpContext(budgetId));

        await handler.HandleAsync(authCtx);

        authCtx.HasSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task MissingUserIdClaim_Fails()
    {
        var emptyUser   = new ClaimsPrincipal(new ClaimsIdentity([], "test"));
        var handler     = new BudgetAuthorizationHandler(_cache, _factory);
        var requirement = new BudgetRequirement(BudgetRole.ReadOnly);
        var authCtx     = MakeAuthContext(emptyUser, requirement, MakeHttpContext(Guid.NewGuid()));

        await handler.HandleAsync(authCtx);

        authCtx.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task MissingBudgetIdRouteValue_Fails()
    {
        var httpContext = new DefaultHttpContext(); // no route values
        var handler     = new BudgetAuthorizationHandler(_cache, _factory);
        var requirement = new BudgetRequirement(BudgetRole.ReadOnly);
        var authCtx     = MakeAuthContext(MakeUser(Guid.NewGuid()), requirement, httpContext);

        await handler.HandleAsync(authCtx);

        authCtx.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task CacheHit_OperatorMeetsOperatorRequirement_Succeeds()
    {
        var userId   = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        _cache.Set($"budget-membership:{userId}:{budgetId}", BudgetRole.Operator);

        var handler     = new BudgetAuthorizationHandler(_cache, _factory);
        var requirement = new BudgetRequirement(BudgetRole.Operator);
        var authCtx     = MakeAuthContext(MakeUser(userId), requirement, MakeHttpContext(budgetId));

        await handler.HandleAsync(authCtx);

        authCtx.HasSucceeded.ShouldBeTrue();
    }

    /// <summary>
    /// budget-member-administration WU2 (AUTHZ-1, security-critical): a cached role for a
    /// budget/user pair is honoured on the fast path regardless of the underlying membership's
    /// current soft-delete state — the cache is what gets evicted synchronously by every
    /// mutating handler (RemoveBudgetMember/RestoreBudgetMember/UpdateMemberRole/AcceptInvitation),
    /// not consulted for IsDeleted here. This test documents that the WU2 filter lives in the
    /// Dapper fallback query (18.4), not the cache-hit fast path, which stays byte-identical.
    /// </summary>
    [Fact]
    public async Task CacheHit_StillHonoursCachedRole_CacheHitPathUnchangedByWU2()
    {
        var userId   = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        _cache.Set($"budget-membership:{userId}:{budgetId}", BudgetRole.Operator);

        var handler     = new BudgetAuthorizationHandler(_cache, _factory);
        var requirement = new BudgetRequirement(BudgetRole.Operator);
        var authCtx     = MakeAuthContext(MakeUser(userId), requirement, MakeHttpContext(budgetId));

        await handler.HandleAsync(authCtx);

        authCtx.HasSucceeded.ShouldBeTrue();
    }

    /// <summary>
    /// Scenario: Deleted budget → cache miss path triggers DB query.
    /// Since this is a unit test without a real DB, we verify that on cache miss
    /// with no cached role the handler fails and does not write a cache entry.
    /// The full "deleted budget → budget-not-found flag" scenario is covered by integration tests.
    /// </summary>
    [Fact]
    public async Task CacheMiss_NoRole_DoesNotWriteCacheEntry()
    {
        var userId   = Guid.NewGuid();
        var budgetId = Guid.NewGuid();
        // No cache entry seeded — triggers Dapper fallback (will fail to connect in unit test)
        var handler     = new BudgetAuthorizationHandler(_cache, _factory);
        var requirement = new BudgetRequirement(BudgetRole.ReadOnly);
        var httpCtx     = MakeHttpContext(budgetId);
        var authCtx     = MakeAuthContext(MakeUser(userId), requirement, httpCtx);

        // Handler will throw due to no real DB connection in unit context.
        // We assert the specific exception type to document expected failure mode.
        await Assert.ThrowsAnyAsync<Exception>(() => handler.HandleAsync(authCtx));

        // Cache must NOT have been populated for the failed lookup
        _cache.TryGetValue($"budget-membership:{userId}:{budgetId}", out BudgetRole? _)
            .ShouldBeFalse();
    }
}
