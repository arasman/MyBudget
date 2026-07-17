using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.RestoreBudget;

public sealed class RestoreBudgetHandler
    : IRequestHandler<RestoreBudgetCommand, Result<RestoreBudgetResponse>>
{
    private readonly AppDbContext      _db;
    private readonly ConnectionFactory _factory;
    private readonly IMemoryCache      _cache;
    private readonly ILogger<RestoreBudgetHandler> _logger;

    public RestoreBudgetHandler(
        AppDbContext db,
        ConnectionFactory factory,
        IMemoryCache cache,
        ILogger<RestoreBudgetHandler> logger)
    {
        _db      = db;
        _factory = factory;
        _cache   = cache;
        _logger  = logger;
    }

    public async ValueTask<Result<RestoreBudgetResponse>> Handle(
        RestoreBudgetCommand cmd, CancellationToken ct)
    {
        // Manual ownership check via Dapper — this endpoint bypasses the budget:owner policy
        // because the auth handler returns 404 for deleted budgets; ownership must be checked here.
        using var conn = _factory.CreateConnection();
        var roleValue = await conn.QuerySingleOrDefaultAsync<int?>(
            """
            SELECT "Role"
            FROM "BudgetMemberships"
            WHERE "BudgetId" = @BudgetId AND "UserId" = @UserId
            LIMIT 1
            """,
            new { BudgetId = cmd.BudgetId, UserId = cmd.UserId });

        if (roleValue is null || (BudgetRole)roleValue.Value < BudgetRole.Owner)
            return Result<RestoreBudgetResponse>.Failure("BUDGET_NOT_FOUND");

        // Load the budget including soft-deleted ones (IgnoreQueryFilters not needed since
        // Budget has no global EF query filter — we simply load it by Id)
        var budget = await _db.Budgets.FirstOrDefaultAsync(
            b => b.Id == cmd.BudgetId, ct);

        if (budget is null)
            return Result<RestoreBudgetResponse>.Failure("BUDGET_NOT_FOUND");

        if (!budget.IsDeleted)
            return Result<RestoreBudgetResponse>.Failure("BUDGET_NOT_DELETED");

        // Collect member IDs before restore for cache eviction
        var memberIds = (await conn.QueryAsync<Guid>(
            """
            SELECT "UserId" FROM "BudgetMemberships" WHERE "BudgetId" = @BudgetId
            """,
            new { BudgetId = cmd.BudgetId })).ToList();

        budget.Restore();
        await _db.SaveChangesAsync(ct);

        // Evict cache for all members
        foreach (var userId in memberIds)
            _cache.Remove($"budget-membership:{userId}:{cmd.BudgetId}");

        _logger.LogInformation(
            "Budget {BudgetId} restored by user {UserId}", cmd.BudgetId, cmd.UserId);

        return Result<RestoreBudgetResponse>.Success(new RestoreBudgetResponse(budget.Id, budget.Name));
    }
}
