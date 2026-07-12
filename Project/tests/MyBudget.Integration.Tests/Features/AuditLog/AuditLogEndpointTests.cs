using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.AuditLog;

/// <summary>
/// Integration tests for GET /budgets/{id}/audit-log and
/// GET /budgets/{id}/security-audit-log.
/// PR4 tasks 4.7 – 4.11.
/// </summary>
public sealed class AuditLogEndpointTests : IntegrationTestBase
{
    public AuditLogEndpointTests(IntegrationTestFactory factory) : base(factory) { }

    // -------------------------------------------------------------------------
    // 4.7 — Admin calls GET /budgets/{id}/audit-log → 200 OK paginated entries
    // -------------------------------------------------------------------------

    [Fact]
    public async Task AdminCaller_GetAuditLog_Returns200WithPaginatedEntries()
    {
        // Register user (owner) and get budget
        var login    = await RegisterUserAsync("audit-admin1@example.com");
        AuthorizeClient(login.AccessToken);
        var budgetId = await GetBudgetIdAsync();

        // Produce at least one audit row by creating a category group via the API
        var me = await Client.GetAsync("/api/auth/me");
        await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups",
            new { name = "Housing", displayOrder = 1 });

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/audit-log");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<AuditLogItemDto>>(JsonOpts);
        body.ShouldNotBeNull();
        body!.TotalCount.ShouldBeGreaterThan(0);
        body.Items.ShouldNotBeEmpty();
        body.Page.ShouldBe(1);
        body.PageSize.ShouldBe(20);
    }

    // -------------------------------------------------------------------------
    // 4.8 — Member calls GET /budgets/{id}/audit-log → 403 Forbidden
    // -------------------------------------------------------------------------

    [Fact]
    public async Task MemberCaller_GetAuditLog_Returns403()
    {
        var ownerLogin = await RegisterUserAsync("audit-owner2@example.com");
        AuthorizeClient(ownerLogin.AccessToken);
        var budgetId = await GetBudgetIdAsync();

        // Register a member (Operator role, below Admin)
        var memberLogin = await RegisterUserAsync("audit-member2@example.com");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var membership = BudgetMembership.Create(budgetId, memberLogin.User.Id, BudgetRole.Operator);
            db.BudgetMemberships.Add(membership);
            await db.SaveChangesAsync();
        }

        AuthorizeClient(memberLogin.AccessToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetId}/audit-log");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // 4.9 — Filter by entityName + date range returns only matching rows
    // -------------------------------------------------------------------------

    [Fact]
    public async Task FilterByEntityNameAndDateRange_ReturnsOnlyMatchingRows()
    {
        var login    = await RegisterUserAsync("audit-filter3@example.com");
        AuthorizeClient(login.AccessToken);
        var budgetId = await GetBudgetIdAsync();

        // Create a CategoryGroup (produces AuditLog with EntityName = "CategoryGroup")
        await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups",
            new { name = "Transport", displayOrder = 1 });

        var groupResponse = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups",
            new { name = "Food", displayOrder = 2 });
        groupResponse.EnsureSuccessStatusCode();
        var groupBody = await groupResponse.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);

        // Create a Category (EntityName = "Category")
        await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupBody!.Id}/categories",
            new { name = "Groceries", displayOrder = 1 });

        // Filter: only CategoryGroup entries
        // Use Uri.EscapeDataString to safely encode DateTimeOffset ISO 8601 strings ('+' sign must be encoded).
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(-5).ToString("o"));
        var to   = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddMinutes(5).ToString("o"));

        var response = await Client.GetAsync(
            $"/api/budgets/{budgetId}/audit-log?entityName=CategoryGroup&from={from}&to={to}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<AuditLogItemDto>>(JsonOpts);
        body.ShouldNotBeNull();
        // All returned items must be CategoryGroup
        body!.Items.ShouldAllBe(i => i.EntityName == "CategoryGroup");
        // No Category items
        body.Items.ShouldNotContain(i => i.EntityName == "Category");
        body.TotalCount.ShouldBeGreaterThan(0);
    }

    // -------------------------------------------------------------------------
    // 4.10 — Owner calls GET /budgets/{id}/security-audit-log → 200 OK
    //        Only events from budget members included; non-members excluded
    // -------------------------------------------------------------------------

    [Fact]
    public async Task OwnerCaller_GetSecurityAuditLog_Returns200_OnlyMemberEvents()
    {
        // Setup budget owner (member of budgetA)
        var ownerLogin = await RegisterUserAsync("audit-sal-owner4@example.com");
        AuthorizeClient(ownerLogin.AccessToken);
        var budgetIdA = await GetBudgetIdAsync();

        // Register a second user (member of budgetA)
        var memberLogin = await RegisterUserAsync("audit-sal-member4@example.com");
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var membership = BudgetMembership.Create(budgetIdA, memberLogin.User.Id, BudgetRole.Admin);
            db.BudgetMemberships.Add(membership);
            await db.SaveChangesAsync();
        }

        // Register a third user who is NOT a member of budgetA
        var outsiderLogin = await RegisterUserAsync("audit-sal-outsider4@example.com");

        // All three users have SecurityAuditLog rows from registration (AccountRegistered)
        // Verify the endpoint only returns member events (owner + member; NOT outsider)
        AuthorizeClient(ownerLogin.AccessToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetIdA}/security-audit-log");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PagedResult<SecurityAuditLogItemDto>>(JsonOpts);
        body.ShouldNotBeNull();

        var returnedUserIds = body!.Items
            .Where(i => i.UserId.HasValue)
            .Select(i => i.UserId!.Value)
            .Distinct()
            .ToHashSet();

        // Owner and member should be present
        returnedUserIds.ShouldContain(ownerLogin.User.Id);
        returnedUserIds.ShouldContain(memberLogin.User.Id);

        // Outsider should NOT be present
        returnedUserIds.ShouldNotContain(outsiderLogin.User.Id);
    }

    // -------------------------------------------------------------------------
    // 4.11 — Non-member calls GET /budgets/{id}/security-audit-log → 403
    // -------------------------------------------------------------------------

    [Fact]
    public async Task NonMemberCaller_GetSecurityAuditLog_Returns403()
    {
        var ownerLogin = await RegisterUserAsync("audit-sal-owner5@example.com");
        AuthorizeClient(ownerLogin.AccessToken);
        var budgetId = await GetBudgetIdAsync();

        // Register a user who has no membership in this budget
        var outsiderLogin = await RegisterUserAsync("audit-sal-outsider5@example.com");
        AuthorizeClient(outsiderLogin.AccessToken);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/security-audit-log");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private async Task<Guid> GetBudgetIdAsync()
    {
        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        return meBody!.Memberships[0].BudgetId;
    }

    private sealed record IdResponse(Guid Id);
    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role);

    private sealed record PagedResult<T>(
        List<T> Items,
        int     TotalCount,
        int     Page,
        int     PageSize);

    private sealed record AuditLogItemDto(
        Guid            Id,
        string          EntityName,
        Guid            EntityId,
        string          Action,
        Guid?           UserId,
        DateTimeOffset  Timestamp,
        string?         BeforeJson,
        string?         AfterJson,
        Guid?           BudgetId);

    private sealed record SecurityAuditLogItemDto(
        Guid            Id,
        string          Event,
        Guid?           UserId,
        string?         Email,
        string?         IpAddress,
        string?         UserAgent,
        DateTimeOffset  Timestamp,
        string?         Details);
}
