using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace MyBudget.Integration.Tests.Features.BudgetStructure;

/// <summary>
/// Integration tests for Category endpoints.
/// Covers REQ-CAT-01 to REQ-CAT-04.
/// </summary>
public sealed class CategoryTests : BudgetStructureTestBase
{
    public CategoryTests(Infrastructure.IntegrationTestFactory factory) : base(factory) { }

    // ── REQ-CAT-01: Create Category ───────────────────────────────────────────

    [Fact]
    public async Task CreateCategory_HappyPath_Returns201()
    {
        var (_, budgetId) = await SetupOwnerAsync("cat-create1@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId, "Housing");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupId}/categories",
            new { name = "Rent", displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IdResponse>(JsonOpts);
        body!.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateCategory_DuplicateNameInGroup_Returns422WithCode()
    {
        var (_, budgetId) = await SetupOwnerAsync("cat-create2@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId, "Housing");
        await CreateCategoryAsync(budgetId, groupId, "Rent");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupId}/categories",
            new { name = "Rent", displayOrder = 2 });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("CATEGORY_NAME_DUPLICATE");
    }

    [Fact]
    public async Task CreateCategory_Unauthenticated_Returns401()
    {
        Client.DefaultRequestHeaders.Remove("Authorization");

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{Guid.NewGuid()}/category-groups/{Guid.NewGuid()}/categories",
            new { name = "Rent", displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateCategory_ViewerRole_Returns403()
    {
        var (_, budgetId) = await SetupOwnerAsync("cat-create4-owner@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId, "Housing");
        var viewerToken   = await SetupViewerAsync(budgetId, "cat-create4-viewer@example.com");
        AuthorizeClient(viewerToken);

        var response = await Client.PostAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupId}/categories",
            new { name = "Rent", displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ── REQ-CAT-02: Update Category ───────────────────────────────────────────

    [Fact]
    public async Task UpdateCategory_HappyPath_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("cat-update1@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId, "Housing");
        var catId         = await CreateCategoryAsync(budgetId, groupId, "Rent");

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupId}/categories/{catId}",
            new { name = "Rent & Mortgage", displayOrder = 1 });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ── REQ-CAT-03: Delete Category ───────────────────────────────────────────

    [Fact]
    public async Task DeleteCategory_SoftDeleteOnly_Returns204()
    {
        var (_, budgetId) = await SetupOwnerAsync("cat-delete1@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId, "Housing");
        var catId         = await CreateCategoryAsync(budgetId, groupId, "Rent");

        var response = await Client.DeleteAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupId}/categories/{catId}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Verify category gone from group listing
        var listResponse = await Client.GetAsync($"/api/budgets/{budgetId}/category-groups");
        var list = await listResponse.Content.ReadFromJsonAsync<CategoryGroupItem[]>(JsonOpts);
        list!.Single(g => g.Id == groupId).Categories.ShouldBeEmpty();
    }

    // ── REQ-CAT-04: Reorder Categories ───────────────────────────────────────

    [Fact]
    public async Task ReorderCategories_HappyPath_Returns200()
    {
        var (_, budgetId) = await SetupOwnerAsync("cat-reorder1@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId, "Housing");
        var c1 = await CreateCategoryAsync(budgetId, groupId, "A", 1);
        var c2 = await CreateCategoryAsync(budgetId, groupId, "B", 2);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupId}/categories/order",
            new { orderedIds = new[] { c2, c1 } });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ReorderCategories_IncompleteList_Returns422()
    {
        var (_, budgetId) = await SetupOwnerAsync("cat-reorder2@example.com");
        var groupId       = await CreateCategoryGroupAsync(budgetId, "Housing");
        var c1 = await CreateCategoryAsync(budgetId, groupId, "A", 1);
        await CreateCategoryAsync(budgetId, groupId, "B", 2);

        var response = await Client.PutAsJsonAsync(
            $"/api/budgets/{budgetId}/category-groups/{groupId}/categories/order",
            new { orderedIds = new[] { c1 } });

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOpts);
        body!.Error.ShouldBe("REORDER_LIST_INCOMPLETE");
    }

    // ── Response helpers ──────────────────────────────────────────────────────

    private sealed record ErrorResponse(string Error);
    private sealed record CategoryGroupItem(Guid Id, string Name, int DisplayOrder, CategoryItem[] Categories);
    private sealed record CategoryItem(Guid Id, string Name, int DisplayOrder);
}
