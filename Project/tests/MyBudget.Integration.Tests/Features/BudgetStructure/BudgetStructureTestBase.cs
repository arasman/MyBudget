using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetStructure;

/// <summary>
/// Shared helpers for BudgetStructure integration tests.
/// Provides typed setup methods for creating budgets, cycles, periods, groups, categories, and lines.
/// </summary>
public abstract class BudgetStructureTestBase : IntegrationTestBase
{
    protected BudgetStructureTestBase(IntegrationTestFactory factory) : base(factory) { }

    // ── Auth helpers ─────────────────────────────────────────────────────────

    /// <summary>
    /// Registers a user, authenticates the client, and returns the budget ID auto-created on registration.
    /// </summary>
    protected async Task<(string AccessToken, Guid BudgetId)> SetupOwnerAsync(
        string email = "owner@example.com")
    {
        var login = await RegisterUserAsync(email);
        AuthorizeClient(login.AccessToken);

        var me     = await Client.GetAsync("/api/auth/me");
        var meBody = await me.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        meBody.ShouldNotBeNull("Me response body should not be null");

        return (login.AccessToken, meBody!.Memberships[0].BudgetId);
    }

    /// <summary>Registers a viewer user and adds them as a Viewer member of the given budget.</summary>
    protected async Task<string> SetupViewerAsync(Guid budgetId, string email = "viewer@example.com")
    {
        var login = await RegisterUserAsync(email);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var membership = BudgetMembership.Create(budgetId, login.User.Id, BudgetRole.ReadOnly);
        db.BudgetMemberships.Add(membership);
        await db.SaveChangesAsync();

        return login.AccessToken;
    }

    // ── Cycle helpers ─────────────────────────────────────────────────────────

    protected async Task<Guid> CreateCycleAsync(
        Guid     budgetId,
        string   name      = "Cycle 2025",
        DateOnly? start    = null,
        DateOnly? end      = null)
    {
        var s = start ?? new DateOnly(2025, 1, 1);
        var e = end   ?? new DateOnly(2025, 12, 31);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles",
            new { name, startDate = s, endDate = e, defaultCurrencyId = CurrencySeeds.GtqId });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        return body!.Id;
    }

    // ── Period helpers ────────────────────────────────────────────────────────

    protected async Task<Guid> CreatePeriodAsync(
        Guid     budgetId,
        Guid     cycleId,
        string   name         = "January",
        int      periodNumber = 1,
        DateOnly? start       = null,
        DateOnly? end         = null)
    {
        var s = start ?? new DateOnly(2025, 1, 1);
        var e = end   ?? new DateOnly(2025, 1, 31);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/cycles/{cycleId}/periods",
            new { name, periodNumber, startDate = s, endDate = e });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        return body!.Id;
    }

    // ── CategoryGroup helpers ─────────────────────────────────────────────────

    protected async Task<Guid> CreateCategoryGroupAsync(
        Guid   budgetId,
        string name         = "Housing",
        int    displayOrder = 1)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups",
            new { name, displayOrder });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        return body!.Id;
    }

    // ── Category helpers ──────────────────────────────────────────────────────

    protected async Task<Guid> CreateCategoryAsync(
        Guid   budgetId,
        Guid   groupId,
        string name         = "Rent",
        int    displayOrder = 1)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupId}/categories",
            new { name, displayOrder });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        return body!.Id;
    }

    // ── BudgetLine helpers ────────────────────────────────────────────────────

    protected async Task<Guid> CreateBudgetLineAsync(
        Guid    budgetId,
        Guid    periodId,
        Guid    categoryGroupId,
        string  name       = "Rent",
        string  lineType   = "Expense",
        decimal amount     = 1500m,
        Guid?   currencyId = null,
        Guid?   categoryId = null)
    {
        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/periods/{periodId}/lines",
            new
            {
                name,
                lineType,
                isRecurring     = false,
                categoryGroupId,
                categoryId,
                budgetedAmount  = amount,
                currencyId,
            });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        return body!.Id;
    }

    // ── Response types ────────────────────────────────────────────────────────

    protected sealed record IdResponse(Guid Id);
    protected sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    protected sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role);
}

