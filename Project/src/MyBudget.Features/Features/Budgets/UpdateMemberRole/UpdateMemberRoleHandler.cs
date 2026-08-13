using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Auth.Authorization;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.UpdateMemberRole;

public sealed class UpdateMemberRoleHandler
    : IRequestHandler<UpdateMemberRoleCommand, Result<UpdateMemberRoleResponse>>
{
    private readonly AppDbContext      _db;
    private readonly ConnectionFactory _factory;
    private readonly IMemoryCache      _cache;
    private readonly ILogger<UpdateMemberRoleHandler> _logger;

    public UpdateMemberRoleHandler(
        AppDbContext db,
        ConnectionFactory factory,
        IMemoryCache cache,
        ILogger<UpdateMemberRoleHandler> logger)
    {
        _db      = db;
        _factory = factory;
        _cache   = cache;
        _logger  = logger;
    }

    public async ValueTask<Result<UpdateMemberRoleResponse>> Handle(
        UpdateMemberRoleCommand cmd, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // One Dapper round trip for both actor and target rows (design's Permission matrix note).
        var rows = (await conn.QueryAsync<MembershipRow>(
            """
            SELECT "UserId", "Role"
            FROM "BudgetMemberships"
            WHERE "BudgetId" = @BudgetId AND "UserId" IN (@ActorId, @TargetId)
            """,
            new { BudgetId = cmd.BudgetId, ActorId = cmd.ActorUserId, TargetId = cmd.TargetUserId }))
            .ToList();

        // The actor's row is guaranteed to exist — BudgetAuthorizationHandler already resolved
        // budget:admin for the actor before this handler runs.
        var actorRow  = rows.FirstOrDefault(r => r.UserId == cmd.ActorUserId);
        var targetRow = rows.FirstOrDefault(r => r.UserId == cmd.TargetUserId);

        if (actorRow is null)
            return Result<UpdateMemberRoleResponse>.Failure("MEMBERS_NOT_FOUND");

        var actorRole  = (BudgetRole)actorRow.Role;
        BudgetRole? targetRole = targetRow is null ? null : (BudgetRole)targetRow.Role;

        var errorCode = MemberActionPolicy.Evaluate(
            cmd.ActorUserId, actorRole, cmd.TargetUserId, targetRole, cmd.NewRole);

        if (errorCode is not null)
            return Result<UpdateMemberRoleResponse>.Failure(errorCode);

        var membership = await _db.BudgetMemberships.FirstOrDefaultAsync(
            m => m.BudgetId == cmd.BudgetId && m.UserId == cmd.TargetUserId, ct);

        if (membership is null)
            return Result<UpdateMemberRoleResponse>.Failure("MEMBERS_NOT_FOUND");

        // No public domain method exists yet for changing an existing membership's role — that
        // lands with BudgetMembership's soft-delete rework in WU2 (ChangeRole). Update the tracked
        // property directly via EF's change tracker in the meantime.
        _db.Entry(membership).Property(m => m.Role).CurrentValue = cmd.NewRole;
        await _db.SaveChangesAsync(ct);

        // Evict the target's cached role (MEM-SC-3) before returning.
        _cache.Remove($"budget-membership:{cmd.TargetUserId}:{cmd.BudgetId}");

        _logger.LogInformation(
            "Membership role for user {TargetUserId} in budget {BudgetId} changed to {NewRole} by user {ActorUserId}",
            cmd.TargetUserId, cmd.BudgetId, cmd.NewRole, cmd.ActorUserId);

        return Result<UpdateMemberRoleResponse>.Success(
            new UpdateMemberRoleResponse(cmd.TargetUserId, cmd.NewRole.ToApiString()));
    }

    private sealed record MembershipRow(Guid UserId, int Role);
}
