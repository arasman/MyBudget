using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetStructure;

/// <summary>
/// Integration tests for CategoryGroup endpoints.
/// Covers REQ-CG-01 to REQ-CG-04.
/// </summary>
public sealed class CategoryGroupTests : BudgetStructureTestBase
{
    public CategoryGroupTests(Infrastructure.IntegrationTestFactory factory) : base(factory) { }

    // ── REQ-CG-01: Create CategoryGroup ──────────────────────────────────────

    [Fact]
    public async Task CreateCategoryGroup_HappyPath_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("cg-create1@example.com");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups",
            new { name = "Housing", displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        body!.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateCategoryGroup_DuplicateName_Returns422WithCode()
    {
        var (_, budgetId) = await SetupOwnerAsync("cg-create2@example.com");
        await CreateCategoryGroupAsync(budgetId, "Housing");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups",
            new { name = "Housing", displayOrder = 2 });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("CATEGORY_GROUP_NAME_DUPLICATE");
    }

    [Fact]
    public async Task CreateCategoryGroup_Unauthenticated_Returns401()
    {
        Client.DefaultRequestHeaders.Remove("Authorization");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{Guid.NewGuid()}/category-groups",
            new { name = "Housing", displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCategoryGroup_ViewerRole_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("cg-create4-owner@example.com");
        var viewerToken   = await SetupViewerAsync(budgetId, "cg-create4-viewer@example.com");
        AuthorizeClient(viewerToken);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups",
            new { name = "Housing", displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── REQ-CG-02: Update CategoryGroup ──────────────────────────────────────

    [Fact]
    public async Task UpdateCategoryGroup_HappyPath_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("cg-update1@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId, "Housing");

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupId}",
            new { name = "Home & Utilities", displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── REQ-CG-03: Delete CategoryGroup ──────────────────────────────────────

    [Fact]
    public async Task DeleteCategoryGroup_CascadesToCategories_Returns204()
    {
        var (_, budgetId) = await SetupOwnerAsync("cg-delete1@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId, "Housing");
        await CreateCategoryAsync(budgetId, groupId, "Rent");
        await CreateCategoryAsync(budgetId, groupId, "Electricity");

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify group gone from list endpoint
        var listResponse = await Client.GetAsync($"/api/budgets/{budgetId}/category-groups");
        var list = await listResponse.Content.ReadFromJsonAsync<CategoryGroupItem[]>(JsonOpts);
        list!.ShouldBeEmpty();
    }

    // ── REQ-CG-04: Reorder CategoryGroups ────────────────────────────────────

    [Fact]
    public async Task ReorderCategoryGroups_HappyPath_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("cg-reorder1@example.com");
        var g1 = await CreateCategoryGroupAsync(budgetId, "A", 1);
        var g2 = await CreateCategoryGroupAsync(budgetId, "B", 2);
        var g3 = await CreateCategoryGroupAsync(budgetId, "C", 3);

        // Reverse order
        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/order",
            new { orderedIds = new[] { g3, g2, g1 } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify via read
        var listResponse = await Client.GetAsync($"/api/budgets/{budgetId}/category-groups");
        var list = await listResponse.Content.ReadFromJsonAsync<CategoryGroupItem[]>(JsonOpts);
        list![0].Id.ShouldBe(g3);
        list![1].Id.ShouldBe(g2);
        list![2].Id.ShouldBe(g1);
    }

    [Fact]
    public async Task ReorderCategoryGroups_IncompleteList_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("cg-reorder2@example.com");
        var g1 = await CreateCategoryGroupAsync(budgetId, "A", 1);
        var g2 = await CreateCategoryGroupAsync(budgetId, "B", 2);
        await CreateCategoryGroupAsync(budgetId, "C", 3);

        // Only 2 of 3 — incomplete
        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/order",
            new { orderedIds = new[] { g1, g2 } });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("REORDER_LIST_INCOMPLETE");
    }

    // ── Response helpers ──────────────────────────────────────────────────────

    private sealed record ErrorResponse(string Error);
    private sealed record CategoryGroupItem(Guid Id, string Name, int DisplayOrder);
}
