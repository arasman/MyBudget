using Dapper;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using MyBudget.Features.SharedKernel.Auth.Authorization;
using MyBudget.Features.SharedKernel.Entities;
using MyBudget.Features.SharedKernel.Persistence;
using MyBudget.Features.SharedKernel.Results;

namespace MyBudget.Features.Features.Budgets.RestoreBudgetMember;

public sealed class RestoreBudgetMemberHandler
    : IRequestHandler<RestoreBudgetMemberCommand, Result<RestoreBudgetMemberResponse>>
{
    private readonly AppDbContext      _db;
    private readonly ConnectionFactory _factory;
    private readonly IMemoryCache      _cache;
    private readonly ILogger<RestoreBudgetMemberHandler> _logger;

    public RestoreBudgetMemberHandler(
        AppDbContext db,
        ConnectionFactory factory,
        IMemoryCache cache,
        ILogger<RestoreBudgetMemberHandler> logger)
    {
        _db      = db;
        _factory = factory;
        _cache   = cache;
        _logger  = logger;
    }

    public async ValueTask<Result<RestoreBudgetMemberResponse>> Handle(
        RestoreBudgetMemberCommand cmd, CancellationToken ct)
    {
        using var conn = _factory.CreateConnection();

        // One Dapper round trip for both actor and target rows — includes soft-deleted target
        // rows on purpose (unlike RemoveBudgetMemberHandler): the permission matrix here is
        // applied using the role the member HELD BEFORE removal (spec MEMBERS-RESTORE-1), so the
        // target's pre-removal role must be visible to MemberActionPolicy.Evaluate.
        var rows = (await conn.QueryAsync<MembershipRow>(
            """
            SELECT "UserId", "Role", "IsDeleted"
            FROM "BudgetMemberships"
            WHERE "BudgetId" = @BudgetId AND "UserId" IN (@ActorId, @TargetId)
            """,
            new { BudgetId = cmd.BudgetId, ActorId = cmd.ActorUserId, TargetId = cmd.TargetUserId }))
            .ToList();

        // The actor's row is guaranteed to exist and be active — this endpoint intentionally uses
        // the STANDARD budget:admin policy (not RestoreBudget's manual Dapper bypass). RestoreBudget
        // bypasses because the *actor's own budget* is soft-deleted, so the actor's own budget:admin
        // resolution would 404 through the auth handler's `IsDeleted=false` JOIN before the handler
        // ever runs. Here it is the *target member's* row that is deleted, not the actor's — the
        // actor is an active Owner/Admin of a live budget and resolves normally through the standard
        // policy. Copying RestoreBudget's bypass would drop a working authorization gate for no
        // reason (design decision 5 / task 17.5).
        var actorRow  = rows.FirstOrDefault(r => r.UserId == cmd.ActorUserId);
        var targetRow = rows.FirstOrDefault(r => r.UserId == cmd.TargetUserId);

        if (actorRow is null)
            return Result<RestoreBudgetMemberResponse>.Failure("MEMBERS_NOT_FOUND");

        var actorRole  = (BudgetRole)actorRow.Role;
        BudgetRole? targetRole = targetRow is null ? null : (BudgetRole)targetRow.Role;

        var errorCode = MemberActionPolicy.Evaluate(
            cmd.ActorUserId, actorRole, cmd.TargetUserId, targetRole);

        if (errorCode is not null)
            return Result<RestoreBudgetMemberResponse>.Failure(errorCode);

        var membership = await _db.BudgetMemberships.FirstOrDefaultAsync(
            m => m.BudgetId == cmd.BudgetId && m.UserId == cmd.TargetUserId, ct);

        if (membership is null)
            return Result<RestoreBudgetMemberResponse>.Failure("MEMBERS_NOT_FOUND");

        if (!membership.IsDeleted)
            return Result<RestoreBudgetMemberResponse>.Failure("MEMBERS_NOT_DELETED");

        // Restore does NOT change role — only AcceptInvitation's restore path does that
        // (spec MEMBERS-RESTORE-1 note).
        membership.Restore();
        await _db.SaveChangesAsync(ct);

        // Evict the target's cached role (MEM-SC-3) before returning.
        _cache.Remove($"budget-membership:{cmd.TargetUserId}:{cmd.BudgetId}");

        _logger.LogInformation(
            "Membership for user {TargetUserId} in budget {BudgetId} restored by user {ActorUserId}",
            cmd.TargetUserId, cmd.BudgetId, cmd.ActorUserId);

        return Result<RestoreBudgetMemberResponse>.Success(
            new RestoreBudgetMemberResponse(cmd.TargetUserId, membership.Role.ToApiString()));
    }

    private sealed record MembershipRow(Guid UserId, int Role, bool IsDeleted);
}
