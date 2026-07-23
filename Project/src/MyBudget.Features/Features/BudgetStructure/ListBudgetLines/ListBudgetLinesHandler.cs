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
                   bl."Description",
                   r."BudgetedAmount", r."CurrencyId",
                   c."Code"   AS "CurrencyCode",
                   c."Symbol" AS "CurrencySymbol"
            FROM "BudgetLines" bl
            LEFT JOIN LATERAL (
                SELECT r2."BudgetedAmount", r2."CurrencyId"
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
                // StartDate / EndDate stored as TEXT in PostgreSQL (EF DateOnly → TEXT)
                DateOnly.Parse(r.StartDate),
                r.EndDate is not null ? DateOnly.Parse(r.EndDate) : null,
                r.BudgetedAmount,
                r.CurrencyId,
                r.CurrencyCode,
                r.CurrencySymbol,
                r.Description,
                // DeletedAt is DateTimeOffset? — pass through directly
                r.DeletedAt))
            .ToList();

        return Result<IReadOnlyList<BudgetLineResponse>>.Success(items);
    }

    private sealed class BudgetLineRow
    {
        public Guid             Id              { get; init; }
        public Guid             BudgetId        { get; init; }
        public Guid             CategoryGroupId { get; init; }
        public Guid?            CategoryId      { get; init; }
        public string           Name            { get; init; } = "";
        public int              LineType        { get; init; }
        public int              DisplayOrder    { get; init; }
        // StartDate / EndDate are stored as TEXT in PostgreSQL — Dapper reads them as string
        public string           StartDate       { get; init; } = "";
        public string?          EndDate         { get; init; }
        // DeletedAt is 'timestamp with time zone'; Npgsql returns DateTimeOffset for this type
        public DateTimeOffset?  DeletedAt       { get; init; }
        public decimal?         BudgetedAmount  { get; init; }
        public Guid?            CurrencyId      { get; init; }
        public string?          Description     { get; init; }
        public string?          CurrencyCode    { get; init; }
        public string?          CurrencySymbol  { get; init; }
    }
}
