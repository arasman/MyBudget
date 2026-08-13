# Design: Budget Member Administration

## Technical Approach

One change, three sequential work units, each an independently revertible PR (WU0 → WU1 → WU2).

- **WU0** — two backend edits, no schema, no new files: an existence guard in `AcceptInvitationHandler` and kebab-case role serialization. Deliberately shaped so WU2 only *adds branches*, never rewrites.
- **WU1** — two new read/write vertical slices under `Features/Budgets/` (`ListBudgetMembers`, `UpdateMemberRole`) following the `RenameBudget`/`InviteUserToBudget` shape (`Command|Query` / `Handler` / `Endpoint` / `Validator`), plus one Vue view, one tab, one route, one API module. No schema, no auth-handler touch.
- **WU2** — `BudgetMembership` soft-delete (mirroring `Budget.cs`), one additive migration, two more slices (`RemoveBudgetMember`, `RestoreBudgetMember`), the one-line `BudgetAuthorizationHandler` filter, and the WU0-guard extension to restore-instead-of-insert.

Endpoints are discovered by `MapAllSliceEndpoints()` reflection, so `Program.cs` is untouched.

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|---|---|---|---|
| 1 | WU0 duplicate guard | EF `_db.BudgetMemberships.FirstOrDefaultAsync(m => m.BudgetId == matched.BudgetId && m.UserId == cmd.UserId, ct)` placed **after** the email-match check and **before** `invitation.MarkUsed()`; if found → `Result.Failure("AUTH_ALREADY_MEMBER")` with **no writes at all** | `try/catch (DbUpdateException)`; Dapper check on the already-open `conn` | Unique-index-driven control flow can't distinguish which constraint fired. Returning before `MarkUsed()` means a duplicate click never burns the token. EF (not Dapper) is chosen *because* WU2 needs the tracked entity to call `Restore()` — WU2 then adds branches instead of replacing the query. |
| 2 | WU0 error code / status | Reuse `AUTH_ALREADY_MEMBER`; endpoint maps to `Results.Problem(detail: "AUTH_ALREADY_MEMBER", statusCode: 409)` | New code `AUTH_ALREADY_ACCEPTED` | `InviteUserToBudgetHandler` already emits `AUTH_ALREADY_MEMBER` with identical meaning. `detail:`-shaped problem matches the other invitation errors, which is what `AcceptInvitationView` already reads. |
| 3 | Role-string fix location | New `SharedKernel/Entities/BudgetRoleStrings.cs`: `ToApiString(this BudgetRole)` (`ReadOnly → "read-only"`, else `ToLowerInvariant()`) + `TryParse(string, out BudgetRole)`. `GetCurrentUserHandler:52` calls `ToApiString()`; `InviteUserToBudgetEndpoint.TryParseRole` delegates to `TryParse` | Widening the frontend `roleKeyMap` with a `readonly` alias | The client contract (`"read-only"`) is already the round-trip form the API *accepts*; only the emit side diverged. A client alias would freeze the bug into the contract. One helper prevents WU1's new slices re-inventing a third convention. |
| 4 | Member-action authorization | `budget:admin` policy on all four endpoints **plus** a pure in-handler matrix in `SharedKernel/Auth/Authorization/MemberActionPolicy.cs`: `Evaluate(actorId, actorRole, targetUserId, targetRole, newRole?) → string? errorCode`. Error codes match `specs/budget-members/spec.md`'s tested contract verbatim: `MEMBERS_CANNOT_ACT_ON_SELF`, `MEMBERS_CANNOT_ACT_ON_OWNER`, `MEMBERS_CANNOT_ACT_ON_ADMIN`, `MEMBERS_CANNOT_PROMOTE_TO_OWNER`, `MEMBERS_NOT_FOUND` | A new policy tier; rules inside the FluentValidation validator; duplicating the matrix per slice; this design's own earlier draft names (`MEMBER_SELF_ACTION_FORBIDDEN` / `MEMBER_OWNER_IMMUTABLE` / `AUTH_INSUFFICIENT_ROLE` / `MEMBER_ROLE_OWNER_NOT_ASSIGNABLE` / `MEMBER_NOT_FOUND`) — superseded, see reconciliation note below | ASP.NET policies cannot see the request target, and validators here are shape-only (no DB). Three slices enforce the *same* matrix — duplicating it three times is exactly how a security matrix drifts. Pure function ⇒ unit-testable without a DB. |
| 5 | `RestoreBudgetMember` policy path | **Standard `budget:admin` policy**, *not* `RestoreBudget`'s manual Dapper bypass | Mirroring `RestoreBudget` | `RestoreBudget` bypasses because the *budget* is soft-deleted, so the auth handler's `b."IsDeleted" = false` JOIN 404s the actor. Here the **target's** membership is deleted, not the actor's; the actor is an active Admin/Owner of a live budget and resolves normally. Copying the bypass would drop a working gate for no reason. Documented so reviewers don't flag the asymmetry. |
| 6 | Auth-handler filter | Add exactly `AND bm."IsDeleted" = false` to the Dapper fallback WHERE clause | EF global query filter; a DB view; a second query | The handler is a raw Dapper string; no filter mechanism exists. A view would hide the security-relevant predicate from anyone reading the hot path. |
| 7 | Unique index | Keep `IX_BudgetMemberships_BudgetId_UserId` **total** (unique across soft-deleted rows) | Partial index `WHERE "IsDeleted" = false` | A partial index would let accept insert a *new* row next to the revoked one, splitting membership history and defeating the point of the restore path. Keeping it total makes restore-in-place structurally mandatory. |
| 8 | Entity API | `IsDeleted`, `DeletedAt`, `SoftDelete()`, `Restore()` copied verbatim from `Budget.cs` (same types, same `UpdatedAt` bump), **plus** a separate `ChangeRole(BudgetRole)` | `Restore(BudgetRole role)` overload | Keeps the `Budget` mirror exact. Accept calls `Restore(); ChangeRole(role);` — role comes from the *new* invitation (proposal Q3). `JoinedAt` is deliberately **not** reset: the row is a resumed membership, not a new one. |
| 9 | Route parameter naming | Budget param stays `{id}`; member param is `{userId}` | `/members/{id}` | `BudgetAuthorizationHandler` reads `RouteValues["id"]` to resolve the budget. Naming the member param `id` would silently authorize against the *member's* GUID. Hard requirement. |
| 10 | Frontend row gating | `useRoleGate(budgetId).isAdmin` gates the tab/view; a **local** `canActOn(m)` in `BudgetMembersView.vue` gates each row. Add `isOwner` to `useRoleGate` (additive) | Putting `canActOn` into `useRoleGate` | `isAdmin` cannot express "Admin, except against another Admin". `useRoleGate` is budget-scoped and shared by many views; the member matrix has one consumer. |
| 11 | Route guard | None — the Members route keeps only `requiresAuth`; a non-admin reaching it by URL gets 403 from the API and the view's error state | Per-role router guard | The router has no per-role guards today; adding one creates a second source of truth for a rule the API already owns. |

### Permission matrix (enforced by `MemberActionPolicy`, evaluated in this order)

| Check | Condition | Error code | HTTP |
|---|---|---|---|
| 1. Self | `targetUserId == actorId` | `MEMBERS_CANNOT_ACT_ON_SELF` | 403 |
| 2. Owner target | `targetRole == Owner` | `MEMBERS_CANNOT_ACT_ON_OWNER` | 403 |
| 3. Admin vs Admin | `actorRole == Admin && targetRole == Admin` | `MEMBERS_CANNOT_ACT_ON_ADMIN` | 403 |
| 4. Promote to Owner | `newRole == Owner` (UpdateMemberRole only) | `MEMBERS_CANNOT_PROMOTE_TO_OWNER` | 422 |
| 5. Target missing | no membership row for `(budgetId, userId)` | `MEMBERS_NOT_FOUND` | 404 |

Self is checked first so an Owner acting on themselves gets the accurate message. Each handler resolves actor role + target row in **one** Dapper round trip, then calls `Evaluate`.

**Reconciliation note**: this design initially drafted a second, differently-named set of error codes for this matrix (`MEMBER_SELF_ACTION_FORBIDDEN`, `MEMBER_OWNER_IMMUTABLE`, `AUTH_INSUFFICIENT_ROLE`, `MEMBER_ROLE_OWNER_NOT_ASSIGNABLE`, `MEMBER_NOT_FOUND`) — HTTP statuses and rule order matched `specs/budget-members/spec.md` throughout, but the string codes diverged. `sdd-tasks` caught the drift and defaulted to spec.md's codes (the tested, scenario-level contract) with this design's rule order. Confirmed correct: spec.md's names are final.

## Data Flow

    Role change / removal
    BudgetMembersView ─→ budgetMembers.api ─→ PATCH .../role | DELETE /api/budgets/{id}/members/{userId}
          │                                        │
          │                          budget:admin policy (BudgetAuthorizationHandler)
          │                                        ▼
          │                     Handler: 1 Dapper read (actor role + target row)
          │                                        ▼
          │                          MemberActionPolicy.Evaluate → errorCode?
          │                                        ▼
          │                     EF: ChangeRole() | SoftDelete() | Restore()
          │                                        ▼
          └── refetch list ◄── _cache.Remove($"budget-membership:{targetUserId}:{budgetId}")

    Revoked member's next request (WU2)
    GET /api/budgets/{id}/... ─→ cache MISS (evicted) ─→ Dapper: ... AND bm."IsDeleted" = false
                                                              ▼ no row
                             budget exists & not deleted ⇒ no "budget-not-found" flag ⇒ 403

`BudgetAuthorizationHandler` — **what changes**: one predicate. **What stays identical**: the cache key and 5-min TTL, the cache-hit fast path, the `budget-not-found` 404-vs-403 disambiguation block, the two budget-existence probes, the `>=` role comparison, and "roles are never read from the JWT". Note the handler never negative-caches, so a revoked member re-hits the DB on every request — the only stale-allow window is a pre-existing *positive* entry, which is precisely why every mutating handler must evict.

## File Changes

| File | WU | Action | Description |
|---|---|---|---|
| `Project/src/MyBudget.Features/SharedKernel/Entities/BudgetRoleStrings.cs` | 0 | Create | `ToApiString` / `TryParse` — single role-string convention |
| `.../Features/Auth/GetCurrentUser/GetCurrentUserHandler.cs` | 0 | Modify | Line 52 → `((BudgetRole)m.Role).ToApiString()` |
| `.../Features/Budgets/InviteUserToBudget/InviteUserToBudgetEndpoint.cs` | 0 | Modify | `TryParseRole` delegates to `BudgetRoleStrings.TryParse` |
| `.../Features/Auth/AcceptInvitation/AcceptInvitationHandler.cs` | 0, 2 | Modify | WU0: existence guard. WU2: soft-deleted → `Restore()` + `ChangeRole()` |
| `.../Features/Auth/AcceptInvitation/AcceptInvitationEndpoint.cs` | 0 | Modify | Map `AUTH_ALREADY_MEMBER` → 409 |
| `.../SharedKernel/Auth/Authorization/MemberActionPolicy.cs` | 1 | Create | Pure permission matrix |
| `.../Features/Budgets/ListBudgetMembers/{Query,Handler,Endpoint}.cs` | 1 | Create | Dapper read slice, no Validator |
| `.../Features/Budgets/UpdateMemberRole/{Command,Handler,Endpoint,Validator}.cs` | 1 | Create | Role change + cache eviction |
| `.../SharedKernel/Entities/BudgetMembership.cs` | 2 | Modify | `IsDeleted`/`DeletedAt`/`SoftDelete()`/`Restore()`/`ChangeRole()` |
| `.../Migrations/*_AddBudgetMembershipSoftDelete.cs` | 2 | Create | `IsDeleted bool NOT NULL DEFAULT false`, `DeletedAt timestamptz NULL` |
| `.../Features/Budgets/{RemoveBudgetMember,RestoreBudgetMember}/{Command,Handler,Endpoint}.cs` | 2 | Create | Soft delete / restore + eviction |
| `.../SharedKernel/Auth/Authorization/BudgetAuthorizationHandler.cs` | 2 | Modify | **Hot path** — `AND bm."IsDeleted" = false` |
| `frontend/src/features/budget-structure/api/budgetMembers.api.ts` | 1, 2 | Create | `listMembers` / `updateMemberRole`; WU2 adds `removeMember` / `restoreMember` |
| `frontend/src/features/budget-structure/views/BudgetMembersView.vue` | 1, 2 | Create | List + role select + `canActOn`; WU2 adds `showDeleted` toggle, remove/restore, confirm dialog, `actionInProgress` (copied from `BudgetSelectionView.vue`) |
| `frontend/src/features/budget-structure/components/BudgetTabs.vue` | 1 | Modify | Members tab mirroring the Dashboard block + `MEMBERS_ROUTE_NAMES` + union entry, `v-if="isAdmin"` |
| `frontend/src/router/index.ts` | 1 | Modify | `path: 'members', name: 'BudgetMembers'`, lazy import |
| `frontend/src/features/budget-structure/composables/useRoleGate.ts` | 1 | Modify | Add `isOwner` |
| `frontend/src/i18n/locales/{en,es}.json` | 0, 1, 2 | Modify | `budgetStructure.members.*` + accept-invitation already-member message |

## Interfaces / Contracts

```
GET    /api/budgets/{id}/members?includeDeleted=false     budget:admin
       → { members: [{ userId, email, firstName, lastName, role, joinedAt }] }   # WU1
       # WU2 adds: isDeleted, deletedAt, and honours includeDeleted (additive)
PATCH  /api/budgets/{id}/members/{userId}/role   { role: "admin"|"operator"|"read-only" }
       → { userId, role }
DELETE /api/budgets/{id}/members/{userId}                 → 204
POST   /api/budgets/{id}/members/{userId}/restore         → { userId, role }
```

WU2's extension of the WU0 guard (the load-bearing edit):

```csharp
if (existing is not null && !existing.IsDeleted)
    return Result<AcceptInvitationResponse>.Failure("AUTH_ALREADY_MEMBER");  // no writes

invitation.MarkUsed();

if (existing is not null) { existing.Restore(); existing.ChangeRole((BudgetRole)matched.Role); }
else _db.BudgetMemberships.Add(
        BudgetMembership.Create(matched.BudgetId, cmd.UserId, (BudgetRole)matched.Role));
```

Frontend row gate:

```ts
function canActOn(m: MemberDto): boolean {
  if (!isAdmin.value) return false
  if (m.userId === authStore.user?.id) return false            // D2
  if (m.role === 'owner') return false                         // D3
  if (!isOwner.value && m.role === 'admin') return false       // D1
  return true
}
```

## Testing Strategy

`strict_tdd: true` — RED test before each implementation step; sequencing is `sdd-tasks`' concern.

| Layer | What to Test | Approach |
|---|---|---|
| Unit (backend) | `MemberActionPolicy.Evaluate` — every actor×target×newRole cell, order of checks; `BudgetRoleStrings` round trip (`ToApiString ∘ TryParse == id` for all 4 roles) | xUnit + Shouldly, no DB |
| Integration | Per endpoint: 200 happy path; 403 self / Owner target / Admin→Admin; 404 unknown member; **cache eviction asserted by a second request in the same test**; duplicate-accept → 409 with invitation still unused; revoke → re-invite → accept → 200 with the *new* role; **regression sweep of existing `budget:*`-gated endpoints after the auth-handler edit** | `MyBudget.Integration.Tests` + `IntegrationTestBase` / `RegisterUserAsync` / `AuthorizeClient` |
| Frontend unit | `canActOn` truth table; role `<select>` reads/writes `read-only`; `showDeleted` filtering; `actionInProgress` disabling; API module URLs | Vitest + `@testing-library/vue`, axios mocked |
| E2E | Owner opens Members tab, demotes an Admin, revokes a member, toggles show-deleted, restores | Playwright, full Docker stack |
| i18n | New keys present in EN and ES | existing `i18n/__tests__/locales.spec.ts` |

## Threat Matrix

**N/A** — no shell commands, subprocesses, VCS/PR automation, executable-file classification, or process integration. The matrix's rows (documentation-like paths, git selection, commit/push state, PR commands) have no counterpart here. The authorization surface is covered by the negative integration tests above, not by this matrix.

## Migration / Rollout

One additive migration in WU2 (`AddBudgetMembershipSoftDelete`): `IsDeleted boolean NOT NULL DEFAULT false`, `DeletedAt timestamptz NULL`. Existing rows default to active; no backfill, no downtime, no index change.

**Sequencing** — WU0 → WU1 → WU2 is mandatory, not cosmetic: WU2 edits the `AcceptInvitationHandler` guard *created* in WU0 and extends the view *created* in WU1. Each is a separate PR in a Feature Branch Chain (PR #1 → feature branch, PR #2 → PR #1's branch, PR #3 → PR #2's branch).

| PR | Scope | Est. lines | Verification |
|---|---|---|---|
| 1 (WU0) | `BudgetRoleStrings`, accept guard, endpoint mapping, invite delegate + tests | ~150 | `dotnet test` |
| 2 (WU1) | 2 backend slices, `MemberActionPolicy`, api/view/tab/route/i18n + tests | ~380 | `dotnet test`, `pnpm test`, `pnpm build` |
| 3 (WU2) | entity + migration, 2 slices, **auth-handler filter**, accept-restore, view controls + tests | ~350 | `dotnet test`, `pnpm test`, Playwright |

Combined the change clears 800 lines, so a single PR is not viable. **Do not merge WU2 into WU1** — its value is that the hot-path edit lands in a small, `hot-path`-tagged diff where reviewer attention is concentrated. PR 2 sits near the 400-line budget; if it forecasts over at `sdd-tasks`, split backend slices from the Vue view rather than deferring tests.

**Rollback**: revert in reverse order. WU2's columns may be left in place (unused) after a code revert; reverting the `BudgetAuthorizationHandler` line restores today's behaviour exactly.

## Open Questions — RESOLVED

- [x] **WU0 scope label.** The proposal marks WU0 "backend only", but without a `AUTH_ALREADY_MEMBER` branch in `AcceptInvitationView.vue` (+2 i18n keys) the graceful failure renders as the generic "An error occurred". Design recommended including that ~5-line frontend branch in WU0.
  **Resolution: confirmed in scope.** The frontend branch + 2 i18n keys ship as part of WU0.
- [x] **`AcceptInvitationResponse.Role`** (`AcceptInvitationHandler:105`) still emits PascalCase `"ReadOnly"` via `.ToString()`. `AcceptInvitationView` reads but never renders it, so this was zero-risk today. Recommended switching it to `ToApiString()` in WU0 for one consistent convention.
  **Resolution: confirmed in scope.** WU0 switches this to `BudgetRoleStrings.ToApiString()`, same helper as the ME-1 fix.

**Spec/design reconciliation note**: the spec agent (running in parallel) independently coined `AUTH_INVITATION_ALREADY_MEMBER` for the WU0 guard, diverging from this design's `AUTH_ALREADY_MEMBER` reuse. User resolved in favor of reuse (this design's recommendation, row 2 of the decision table above) — `openspec/changes/budget-member-administration/specs/auth/spec.md` has been corrected to match.
