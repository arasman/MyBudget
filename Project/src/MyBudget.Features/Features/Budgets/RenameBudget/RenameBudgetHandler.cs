using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.RenameBudget;

public sealed class RenameBudgetHandler
    : IRequestHandler<RenameBudgetCommand, Result<RenameBudgetResponse>>
{
    private readonly AppDbContext      _db;
    private readonly ConnectionFactory _factory;
    private readonly IMemoryCache      _cache;
    private readonly ILogger<RenameBudgetHandler> _logger;

    public RenameBudgetHandler(
        AppDbContext db,
        ConnectionFactory factory,
        IMemoryCache cache,
        ILogger<RenameBudgetHandler> logger)
    {
        _db      = db;
        _factory = factory;
        _cache   = cache;
        _logger  = logger;
    }

    public async ValueTask<Result<RenameBudgetResponse>> Handle(
        RenameBudgetCommand cmd, CancellationToken ct)
    {
        var budget = await _db.Budgets.FirstOrDefaultAsync(
            b => b.Id == cmd.BudgetId && !b.IsDeleted, ct);

        if (budget is null)
            return Result<RenameBudgetResponse>.Failure("BUDGET_NOT_FOUND");

        budget.Rename(cmd.NewName);
        await _db.SaveChangesAsync(ct);

        // Evict cache for all members
        await EvictMemberCacheEntriesAsync(cmd.BudgetId);

        _logger.LogInformation(
            "Budget {BudgetId} renamed to '{Name}' by user {UserId}",
            cmd.BudgetId, cmd.NewName, cmd.UserId);

        return Result<RenameBudgetResponse>.Success(new RenameBudgetResponse(budget.Id, budget.Name));
    }

    private async Task EvictMemberCacheEntriesAsync(Guid budgetId)
    {
        using var conn = _factory.CreateConnection();
        var memberIds = (await conn.QueryAsync<Guid>(
            """
            SELECT "UserId" FROM "BudgetMemberships" WHERE "BudgetId" = @BudgetId
            """,
            new { BudgetId = budgetId })).ToList();

        foreach (var userId in memberIds)
            _cache.Remove($"budget-membership:{userId}:{budgetId}");
    }
}
