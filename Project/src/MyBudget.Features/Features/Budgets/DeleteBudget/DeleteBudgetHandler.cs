using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.DeleteBudget;

public sealed class DeleteBudgetHandler
    : IRequestHandler<DeleteBudgetCommand, Result<Unit>>
{
    private readonly AppDbContext      _db;
    private readonly ConnectionFactory _factory;
    private readonly IMemoryCache      _cache;
    private readonly ILogger<DeleteBudgetHandler> _logger;

    public DeleteBudgetHandler(
        AppDbContext db,
        ConnectionFactory factory,
        IMemoryCache cache,
        ILogger<DeleteBudgetHandler> logger)
    {
        _db      = db;
        _factory = factory;
        _cache   = cache;
        _logger  = logger;
    }

    public async ValueTask<Result<Unit>> Handle(
        DeleteBudgetCommand cmd, CancellationToken ct)
    {
        // BudgetAuthorizationHandler already returned 404 for non-existent / deleted budgets.
        // Load the budget; IsDeleted = false is guaranteed by the auth handler's JOIN.
        var budget = await _db.Budgets.FirstOrDefaultAsync(
            b => b.Id == cmd.BudgetId && !b.IsDeleted, ct);

        if (budget is null)
            return Result<Unit>.Failure("BUDGET_NOT_FOUND");

        // Collect member IDs before soft-delete so we can evict cache entries
        using var conn = _factory.CreateConnection();
        var memberIds = (await conn.QueryAsync<Guid>(
            """
            SELECT "UserId" FROM "BudgetMemberships" WHERE "BudgetId" = @BudgetId
            """,
            new { BudgetId = cmd.BudgetId })).ToList();

        budget.SoftDelete();
        await _db.SaveChangesAsync(ct);

        // Evict cache for all members
        foreach (var userId in memberIds)
            _cache.Remove($"budget-membership:{userId}:{cmd.BudgetId}");

        _logger.LogInformation(
            "Budget {BudgetId} soft-deleted by user {UserId}", cmd.BudgetId, cmd.UserId);

        return Result<Unit>.Success(Unit.Value);
    }
}
