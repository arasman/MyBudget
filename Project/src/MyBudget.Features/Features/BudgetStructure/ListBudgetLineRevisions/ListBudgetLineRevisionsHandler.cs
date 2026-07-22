using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.BudgetStructure.ListBudgetLineRevisions;

/// <summary>
/// Returns all revisions for the specified BudgetLine ordered by ValidFrom ASC.
/// REQ-BLR-01: requires budget:admin; line must exist and belong to the budget.
/// </summary>
public sealed class ListBudgetLineRevisionsHandler
    : IRequestHandler<ListBudgetLineRevisionsQuery, Result<IReadOnlyList<RevisionDto>>>
{
    private readonly AppDbContext    _db;
    private readonly ConnectionFactory _factory;

    public ListBudgetLineRevisionsHandler(AppDbContext db, ConnectionFactory factory)
    {
        _db      = db;
        _factory = factory;
    }

    public async ValueTask<Result<IReadOnlyList<RevisionDto>>> Handle(
        ListBudgetLineRevisionsQuery query, CancellationToken ct)
    {
        // Verify line exists and belongs to the budget (includes soft-deleted lines)
        var lineExists = await _db.BudgetLines
            .IgnoreQueryFilters()
            .AnyAsync(l => l.Id == query.LineId && l.BudgetId == query.BudgetId, ct);

        if (!lineExists)
            return Result<IReadOnlyList<RevisionDto>>.Failure("BUDGET_LINE_NOT_FOUND");

        using var conn = _factory.CreateConnection();

        var rows = await conn.QueryAsync<RevisionRow>(
            """
            SELECT r."Id",
                   r."BudgetLineId",
                   r."BudgetedAmount",
                   r."CurrencyId",
                   c."Code"   AS "CurrencyCode",
                   c."Symbol" AS "CurrencySymbol",
                   r."ValidFrom",
                   r."ValidTo",
                   r."Note"
            FROM "BudgetLineRevisions" r
            LEFT JOIN "Currencies" c ON r."CurrencyId" = c."Id"
            WHERE r."BudgetLineId" = @LineId
            ORDER BY r."ValidFrom" ASC
            """,
            new { query.LineId });

        var items = rows
            .Select(r => new RevisionDto(
                r.Id,
                r.BudgetLineId,
                r.BudgetedAmount,
                r.CurrencyId,
                r.CurrencyCode,
                r.CurrencySymbol,
                DateOnly.Parse(r.ValidFrom),
                r.ValidTo is not null ? DateOnly.Parse(r.ValidTo) : null,
                r.Note))
            .ToList();

        return Result<IReadOnlyList<RevisionDto>>.Success(items);
    }

    private sealed class RevisionRow
    {
        public Guid    Id             { get; init; }
        public Guid    BudgetLineId   { get; init; }
        public decimal BudgetedAmount { get; init; }
        public Guid    CurrencyId     { get; init; }
        public string? CurrencyCode   { get; init; }
        public string? CurrencySymbol { get; init; }
        // Stored as TEXT in PostgreSQL — Dapper reads as string
        public string  ValidFrom      { get; init; } = "";
        public string? ValidTo        { get; init; }
        public string? Note           { get; init; }
    }
}
