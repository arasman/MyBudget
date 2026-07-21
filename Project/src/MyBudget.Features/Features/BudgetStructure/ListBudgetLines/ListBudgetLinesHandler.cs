using Dapper;
using Mediator;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListBudgetLines;

/// <summary>
/// Dapper read — returns all BudgetLines for a Budget with their effective revision
/// (ValidFrom &lt;= today AND (ValidTo IS NULL OR ValidTo &gt;= today)).
/// Scoped by BudgetId only — no PeriodId.
/// </summary>
public sealed class ListBudgetLinesHandler
    : IRequestHandler<ListBudgetLinesQuery, Result<IReadOnlyList<BudgetLineResponse>>>
{
    private readonly ConnectionFactory _factory;

    public ListBudgetLinesHandler(ConnectionFactory factory) => _factory = factory;

    public async ValueTask<Result<IReadOnlyList<BudgetLineResponse>>> Handle(
        ListBudgetLinesQuery query, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        var deletedFilter = query.IncludeDeleted ? "" : "AND bl.\"DeletedAt\" IS NULL";
        var today         = DateOnly.FromDateTime(DateTime.UtcNow).ToString("yyyy-MM-dd");

        var rows = await conn.QueryAsync<BudgetLineRow>(
            $"""
            SELECT bl."Id", bl."BudgetId", bl."CategoryGroupId", bl."CategoryId",
                   bl."Name", bl."LineType", bl."DisplayOrder",
                   bl."StartDate", bl."EndDate", bl."DeletedAt",
                   r."BudgetedAmount", r."CurrencyId", r."Note",
                   c."Code"   AS "CurrencyCode",
                   c."Symbol" AS "CurrencySymbol"
            FROM "BudgetLines" bl
            LEFT JOIN LATERAL (
                SELECT r2."BudgetedAmount", r2."CurrencyId", r2."Note"
                FROM "BudgetLineRevisions" r2
                WHERE r2."BudgetLineId" = bl."Id"
                  AND r2."ValidFrom" <= '{today}'
                  AND (r2."ValidTo" IS NULL OR r2."ValidTo" >= '{today}')
                LIMIT 1
            ) r ON true
            LEFT JOIN "Currencies" c ON r."CurrencyId" = c."Id"
            WHERE bl."BudgetId" = @BudgetId {deletedFilter}
            ORDER BY bl."DisplayOrder", bl."Name"
            """,
            new { query.BudgetId });

        var items = rows
            .Select(r => new BudgetLineResponse(
                r.Id,
                r.BudgetId,
                r.CategoryGroupId,
                r.CategoryId,
                r.Name,
                ((LineType)r.LineType).ToString(),
                r.DisplayOrder,
                DateOnly.FromDateTime(r.StartDate),
                r.EndDate.HasValue ? DateOnly.FromDateTime(r.EndDate.Value) : null,
                r.BudgetedAmount,
                r.CurrencyId,
                r.CurrencyCode,
                r.CurrencySymbol,
                r.Note,
                r.DeletedAt.HasValue
                    ? new DateTimeOffset(r.DeletedAt.Value, TimeSpan.Zero)
                    : null))
            .ToList();

        return Result<IReadOnlyList<BudgetLineResponse>>.Success(items);
    }

    private sealed record BudgetLineRow(
        Guid      Id,
        Guid      BudgetId,
        Guid      CategoryGroupId,
        Guid?     CategoryId,
        string    Name,
        int       LineType,
        int       DisplayOrder,
        DateTime  StartDate,
        DateTime? EndDate,
        DateTime? DeletedAt,
        decimal?  BudgetedAmount,
        Guid?     CurrencyId,
        string?   CurrencyCode,
        string?   CurrencySymbol,
        string?   Note);
}
