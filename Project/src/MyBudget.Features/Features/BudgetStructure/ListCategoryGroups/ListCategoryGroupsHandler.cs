using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListCategoryGroups;

/// <summary>
/// Dapper read — two queries: groups ordered by DisplayOrder, then categories for those groups.
/// Nests categories inside groups in-memory before returning.
/// </summary>
public sealed class ListCategoryGroupsHandler
    : IRequestHandler<ListCategoryGroupsQuery, Result<IReadOnlyList<CategoryGroupResponse>>>
{
    private readonly ConnectionFactory _factory;

    public ListCategoryGroupsHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<IReadOnlyList<CategoryGroupResponse>>> Handle(
        ListCategoryGroupsQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        var groupRows = (await conn.QueryAsync<GroupRow>(
            """
            SELECT g."Id", g."Name", g."DisplayOrder"
            FROM "CategoryGroups" g
            WHERE g."BudgetId" = @BudgetId AND g."DeletedAt" IS NULL
            ORDER BY g."DisplayOrder"
            """,
            new { BudgetId = query.BudgetId })).ToList();

        if (groupRows.Count == 0)
            return Result<IReadOnlyList<CategoryGroupResponse>>.Success(
                Array.Empty<CategoryGroupResponse>());

        var groupIds = groupRows.Select(g => g.Id).ToArray();

        var categoryRows = (await conn.QueryAsync<CategoryRow>(
            """
            SELECT c."Id", c."CategoryGroupId", c."Name", c."DisplayOrder"
            FROM "Categories" c
            WHERE c."CategoryGroupId" = ANY(@GroupIds) AND c."DeletedAt" IS NULL
            ORDER BY c."DisplayOrder"
            """,
            new { GroupIds = groupIds })).ToList();

        var categoriesByGroup = categoryRows
            .GroupBy(c => c.CategoryGroupId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => new CategoryItem(c.Id, c.Name, c.DisplayOrder)).ToList());

        var result = groupRows
            .Select(g => new CategoryGroupResponse(
                g.Id,
                g.Name,
                g.DisplayOrder,
                categoriesByGroup.TryGetValue(g.Id, out var cats)
                    ? cats
                    : new List<CategoryItem>()))
            .ToList();

        return Result<IReadOnlyList<CategoryGroupResponse>>.Success(result);
    }

    private sealed record GroupRow(Guid Id, string Name, int DisplayOrder);

    private sealed record CategoryRow(
        Guid   Id,
        Guid   CategoryGroupId,
        string Name,
        int    DisplayOrder);
}
