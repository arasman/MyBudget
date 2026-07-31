using System.Net;
using System.Net.Http.Json;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Dashboard;

/// <summary>
/// Integration tests for GetLifetimeCutTotals endpoint (DASH-1, role scaffold DASH-8).
/// </summary>
public sealed class GetLifetimeCutTotalsIntegrationTests : DashboardTestBase
{
    public GetLifetimeCutTotalsIntegrationTests(IntegrationTestFactory factory) : base(factory) { }

    private sealed record LifetimeCutTotalsResponse(
        string                     ConversionBasis,
        List<CutTotalsPointDto>   Points);

    private sealed record CutTotalsPointDto(
        DateOnly CutDate,
        decimal  ExchangeRate,
        decimal  TotalPositive,     decimal TotalPositiveAlt,
        decimal  TotalNegative,     decimal TotalNegativeAlt,
        decimal  TotalDeudaEnCurso, decimal TotalDeudaEnCursoAlt,
        decimal  TotalBudgeted,     decimal TotalBudgetedAlt,
        decimal  TotalRegistered,   decimal TotalRegisteredAlt,
        decimal  Remaining,         decimal RemainingAlt,
        decimal  TotalAvailable,    decimal TotalAvailableAlt,
        decimal  TotalNet,          decimal TotalNetAlt);

    // ── DASH-1: series shape / ordering ───────────────────────────────────────

    [Fact]
    public async Task GetLifetimeCutTotals_NoCuts_ReturnsEmptySeries()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-lifetime-empty@example.com");

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/cut-totals-series");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LifetimeCutTotalsResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body!.Points.ShouldBeEmpty();
        body.ConversionBasis.ShouldBe("cut-frozen");
    }

    [Fact]
    public async Task GetLifetimeCutTotals_MultipleCuts_ReturnsAllOrderedByCutDateAscending()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-lifetime-multi@example.com");

        // Seeded out of order to prove the query orders, not just returns insertion order.
        await CreateCutRecordAsync(budgetId, new DateOnly(2026, 3, 1), exchangeRate: 7.8m, marker: 300m);
        await CreateCutRecordAsync(budgetId, new DateOnly(2026, 1, 1), exchangeRate: 7.7m, marker: 100m);
        await CreateCutRecordAsync(budgetId, new DateOnly(2026, 2, 1), exchangeRate: 7.75m, marker: 200m);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/cut-totals-series");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LifetimeCutTotalsResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body!.Points.Count.ShouldBe(3);

        body.Points[0].CutDate.ShouldBe(new DateOnly(2026, 1, 1));
        body.Points[0].TotalPositive.ShouldBe(100m);
        body.Points[1].CutDate.ShouldBe(new DateOnly(2026, 2, 1));
        body.Points[1].TotalPositive.ShouldBe(200m);
        body.Points[2].CutDate.ShouldBe(new DateOnly(2026, 3, 1));
        body.Points[2].TotalPositive.ShouldBe(300m);
        body.Points[2].ExchangeRate.ShouldBe(7.8m);
    }

    [Fact]
    public async Task GetLifetimeCutTotals_ScopedToBudgetId_ExcludesOtherBudgetsCuts()
    {
        var (_, budgetIdA) = await SetupOwnerAsync("dash-lifetime-scopeA@example.com");
        var (ownerBToken, budgetIdB) = await SetupOwnerAsync("dash-lifetime-scopeB@example.com");

        await CreateCutRecordAsync(budgetIdA, new DateOnly(2026, 1, 1), marker: 111m);
        await CreateCutRecordAsync(budgetIdB, new DateOnly(2026, 1, 1), marker: 222m);

        AuthorizeClient(ownerBToken);
        var response = await Client.GetAsync($"/api/budgets/{budgetIdB}/dashboard/cut-totals-series");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LifetimeCutTotalsResponse>(JsonOpts);
        body!.Points.ShouldHaveSingleItem();
        body.Points[0].TotalPositive.ShouldBe(222m);
    }

    // ── DASH-8: role-gating scaffold ──────────────────────────────────────────

    [Theory]
    [InlineData("owner")]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("read-only")]
    public async Task GetLifetimeCutTotals_AllFourRoles_Return200(string role)
    {
        var (ownerToken, budgetId) = await SetupOwnerAsync($"dash-role-{role}-owner@example.com");

        var token = role switch
        {
            "owner"     => ownerToken,
            "admin"     => await SetupAdminAsync(budgetId, $"dash-role-{role}-member@example.com"),
            "operator"  => await SetupOperatorAsync(budgetId, $"dash-role-{role}-member@example.com"),
            "read-only" => await SetupViewerAsync(budgetId, $"dash-role-{role}-member@example.com"),
            _           => throw new ArgumentOutOfRangeException(nameof(role)),
        };
        AuthorizeClient(token);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/cut-totals-series");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetLifetimeCutTotals_NoRoleOnBudget_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("dash-role-noaccess-owner@example.com");
        var outsiderLogin = await RegisterUserAsync("dash-role-noaccess-outsider@example.com");
        AuthorizeClient(outsiderLogin.AccessToken);

        var response = await Client.GetAsync($"/api/budgets/{budgetId}/dashboard/cut-totals-series");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
