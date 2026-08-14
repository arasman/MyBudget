using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Auth.Authorization;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.RemoveBudgetMember;

public sealed class RemoveBudgetMemberHandler
    : IRequestHandler<RemoveBudgetMemberCommand, Result<Unit>>
{
    private readonly AppDbContext      _db;
    private readonly ConnectionFactory _factory;
    private readonly IMemoryCache      _cache;
    private readonly ILogger<RemoveBudgetMemberHandler> _logger;

    public RemoveBudgetMemberHandler(
        AppDbContext db,
        ConnectionFactory factory,
        IMemoryCache cache,
        ILogger<RemoveBudgetMemberHandler> logger)
    {
        _db      = db;
        _factory = factory;
        _cache   = cache;
        _logger  = logger;
    }

    public async ValueTask<Result<Unit>> Handle(
        RemoveBudgetMemberCommand cmd, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // One Dapper round trip for both actor and target rows (mirrors UpdateMemberRoleHandler).
        var rows = (await conn.QueryAsync<MembershipRow>(
            """
            SELECT "UserId", "Role", "IsDeleted"
            FROM "BudgetMemberships"
            WHERE "BudgetId" = @BudgetId AND "UserId" IN (@ActorId, @TargetId)
            """,
            new { BudgetId = cmd.BudgetId, ActorId = cmd.ActorUserId, TargetId = cmd.TargetUserId }))
            .ToList();

        // The actor's row is guaranteed to exist and be active — BudgetAuthorizationHandler
        // already resolved budget:admin for the actor before this handler runs.
        var actorRow  = rows.FirstOrDefault(r => r.UserId == cmd.ActorUserId);
        var targetRow = rows.FirstOrDefault(r => r.UserId == cmd.TargetUserId);

        if (actorRow is null)
            return Result<Unit>.Failure("MEMBERS_NOT_FOUND");

        var actorRole = (BudgetRole)actorRow.Role;

        // An already-soft-deleted target does not resolve as a removal target (spec MEMBERS-REMOVE-1
        // "Already-removed member" scenario) — treated identically to no membership row (rule 5).
        BudgetRole? targetRole = (targetRow is null || targetRow.IsDeleted)
            ? null
            : (BudgetRole)targetRow.Role;

        var errorCode = MemberActionPolicy.Evaluate(
            cmd.ActorUserId, actorRole, cmd.TargetUserId, targetRole);

        if (errorCode is not null)
            return Result<Unit>.Failure(errorCode);

        var membership = await _db.BudgetMemberships.FirstOrDefaultAsync(
            m => m.BudgetId == cmd.BudgetId && m.UserId == cmd.TargetUserId && !m.IsDeleted, ct);

        if (membership is null)
            return Result<Unit>.Failure("MEMBERS_NOT_FOUND");

        membership.SoftDelete();
        await _db.SaveChangesAsync(ct);

        // Evict the target's cached role (MEM-SC-3) before returning.
        _cache.Remove($"budget-membership:{cmd.TargetUserId}:{cmd.BudgetId}");

        _logger.LogInformation(
            "Membership for user {TargetUserId} in budget {BudgetId} removed by user {ActorUserId}",
            cmd.TargetUserId, cmd.BudgetId, cmd.ActorUserId);

        return Result<Unit>.Success(Unit.Value);
    }

    private sealed record MembershipRow(Guid UserId, int Role, bool IsDeleted);
}
