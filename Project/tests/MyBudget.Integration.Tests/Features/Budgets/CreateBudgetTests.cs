using System.Net;
using System.Net.Http.Json;
using MyBudget.Integration.Tests.Infrastructure;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.Budgets;

/// <summary>Integration tests for POST /api/budgets.</summary>
public sealed class CreateBudgetTests : IntegrationTestBase
{
    public CreateBudgetTests(IntegrationTestFactory factory) : base(factory) { }

    [Fact]
    public async Task HappyPath_Returns201_WithIdAndName()
    {
        var login = await RegisterUserAsync("create-budget@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.PostAsJsonAsync("/api/budgets", new { name = "Household" });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateBudgetResponse>(JsonOpts);
        body.ShouldNotBeNull();
        body.BudgetId.ShouldNotBe(Guid.Empty);
        body.Name.ShouldBe("Household");

        // Verify BudgetMembership was created with Owner role
        var meResponse = await Client.GetAsync("/api/auth/me");
        meResponse.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>(JsonOpts);
        meBody.ShouldNotBeNull();
        var membership = meBody.Memberships.FirstOrDefault(m => m.BudgetId == body.BudgetId);
        membership.ShouldNotBeNull();
        membership.Role.ShouldBe("owner");
    }

    [Fact]
    public async Task EmptyName_Returns422()
    {
        var login = await RegisterUserAsync("create-budget-empty@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.PostAsJsonAsync("/api/budgets", new { name = "" });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task NameTooLong_Returns422()
    {
        var login = await RegisterUserAsync("create-budget-long@example.com");
        AuthorizeClient(login.AccessToken);

        var response = await Client.PostAsJsonAsync("/api/budgets", new { name = new string('a', 201) });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Unauthenticated_Returns401()
    {
        Client.DefaultRequestHeaders.Remove("Authorization");

        var response = await Client.PostAsJsonAsync("/api/budgets", new { name = "Household" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private sealed record CreateBudgetResponse(Guid BudgetId, string Name);
    private sealed record MeResponse(Guid Id, string Email, MembershipEntry[] Memberships);
    private sealed record MembershipEntry(Guid BudgetId, string BudgetName, string Role, bool IsDeleted);
}
