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

        var sql = query.IncludeDeleted
            ? """
              SELECT g."Id", g."Name", g."DisplayOrder", g."DeletedAt"
              FROM "CategoryGroups" g
              WHERE g."BudgetId" = @BudgetId
              ORDER BY g."DisplayOrder"
              """
            : """
              SELECT g."Id", g."Name", g."DisplayOrder", g."DeletedAt"
              FROM "CategoryGroups" g
              WHERE g."BudgetId" = @BudgetId AND g."DeletedAt" IS NULL
              ORDER BY g."DisplayOrder"
              """;

        var groupRows = (await conn.QueryAsync<GroupRow>(sql,
            new { BudgetId = query.BudgetId })).ToList();

        if (groupRows.Count == 0)
            return Result<IReadOnlyList<CategoryGroupResponse>>.Success(
                Array.Empty<CategoryGroupResponse>());

        var groupIds = groupRows.Select(g => g.Id).ToArray();

        var categorySql = query.IncludeDeleted
            ? """
              SELECT c."Id", c."CategoryGroupId", c."Name", c."DisplayOrder", c."DeletedAt"
              FROM "Categories" c
              WHERE c."CategoryGroupId" = ANY(@GroupIds)
              ORDER BY c."DisplayOrder"
              """
            : """
              SELECT c."Id", c."CategoryGroupId", c."Name", c."DisplayOrder", c."DeletedAt"
              FROM "Categories" c
              WHERE c."CategoryGroupId" = ANY(@GroupIds) AND c."DeletedAt" IS NULL
              ORDER BY c."DisplayOrder"
              """;

        var categoryRows = (await conn.QueryAsync<CategoryRow>(
            categorySql,
            new { GroupIds = groupIds })).ToList();

        var categoriesByGroup = categoryRows
            .GroupBy(c => c.CategoryGroupId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(c => new CategoryItem(
                    c.Id,
                    c.Name,
                    c.DisplayOrder,
                    c.DeletedAt.HasValue ? new DateTimeOffset(c.DeletedAt.Value, TimeSpan.Zero) : null))
                  .ToList());

        var result = groupRows
            .Select(g => new CategoryGroupResponse(
                g.Id,
                g.Name,
                g.DisplayOrder,
                categoriesByGroup.TryGetValue(g.Id, out var cats)
                    ? cats
                    : new List<CategoryItem>(),
                g.DeletedAt.HasValue
                    ? new DateTimeOffset(g.DeletedAt.Value, TimeSpan.Zero)
                    : null))
            .ToList();

        return Result<IReadOnlyList<CategoryGroupResponse>>.Success(result);
    }

    private sealed record GroupRow(Guid Id, string Name, int DisplayOrder, DateTime? DeletedAt);

    private sealed record CategoryRow(
        Guid      Id,
        Guid      CategoryGroupId,
        string    Name,
        int       DisplayOrder,
        DateTime? DeletedAt);
}
