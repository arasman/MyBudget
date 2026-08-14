# Tasks: Budget Member Administration

Strict TDD (`strict_tdd: true`): every implementation step is RED (failing test first) → GREEN
(minimal code to pass) → confirm/REFACTOR. No "implement X" tasks.

**Spec/design divergence — RESOLVED.** `design.md`'s permission-matrix table (Architecture
Decision #4) originally named error codes `MEMBER_SELF_ACTION_FORBIDDEN`, `MEMBER_OWNER_IMMUTABLE`,
`AUTH_INSUFFICIENT_ROLE` (rule 3), `MEMBER_ROLE_OWNER_NOT_ASSIGNABLE`, `MEMBER_NOT_FOUND`, which did
not match `specs/budget-members/spec.md`'s scenario-level codes (`MEMBERS_CANNOT_ACT_ON_SELF`,
`MEMBERS_CANNOT_ACT_ON_OWNER`, `MEMBERS_CANNOT_ACT_ON_ADMIN`, `MEMBERS_CANNOT_PROMOTE_TO_OWNER`,
`MEMBERS_NOT_FOUND`). HTTP statuses and rule order always agreed (403/403/403/422/404). Confirmed:
**spec.md's codes are final** — `design.md` on disk has been corrected to match. This task list
already used spec.md's codes throughout; no task-level changes needed.

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | PR1 ~180, PR2a ~380, PR2b ~330, PR3 ~480 (≈1370 total incl. tests) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 (WU0) → PR2a (WU1 backend) → PR2b (WU1 frontend) → PR3 (WU2) |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

**Why "Decision needed: No" despite High risk:** `design.md`'s Migration/Rollout section already
resolved chain strategy explicitly ("Each is a separate PR in a Feature Branch Chain") and
pre-authorized splitting PR2 if it forecasts over budget ("split backend slices from the Vue view
rather than deferring tests"). WU1's forecast (backend slices + matrix unit tests + two endpoints'
integration tests, each with 403×3/404/422/cache-eviction scenarios) exceeds 400 lines on its own,
so this list splits it into PR2a/PR2b per that pre-authorization — `sdd-apply` should proceed
without re-asking.

### Suggested Work Units

| Unit | Goal | PR | Base branch | Focused test command | Runtime harness | Rollback boundary |
|------|------|----|----|----|----|----|
| WU0 | Duplicate-membership guard, `read-only` serialization fix, accept-error UI | PR1 | `main` | `dotnet test --filter AcceptInvitation\|LogoutAndMe`, `pnpm test -- AcceptInvitationView` | `dotnet test` + Vitest | Revert `BudgetRoleStrings.cs`, `AcceptInvitationHandler.cs`, `AcceptInvitationEndpoint.cs`, `GetCurrentUserHandler.cs`, `InviteUserToBudgetEndpoint.cs`, `AcceptInvitationView.vue`, 2 i18n keys |
| WU1 backend | `MemberActionPolicy` + `ListBudgetMembers` + `UpdateMemberRole` | PR2a | PR1 branch | `dotnet test --filter MemberActionPolicy\|ListBudgetMembers\|UpdateMemberRole` | `dotnet test` | Revert `MemberActionPolicy.cs` + 2 new slice folders; no callers exist yet |
| WU1 frontend | Members tab, view, API module, route | PR2b | PR2a branch | `pnpm test -- BudgetMembersView budgetMembers.api BudgetTabs useRoleGate` | Vitest + `@testing-library/vue` | Revert `BudgetMembersView.vue`, `budgetMembers.api.ts`, `BudgetTabs.vue`, `useRoleGate.ts`, route entry, i18n keys |
| WU2 | Soft-delete entity/migration, Remove/Restore slices, auth-handler filter, accept-restore extension, show-deleted UI | PR3 | PR2b branch | `dotnet test --filter BudgetMembership\|RemoveBudgetMember\|RestoreBudgetMember\|BudgetAuthorizationHandler\|AcceptInvitation`, `pnpm exec playwright test -- budget-structure-members` | `dotnet test` + Vitest + Playwright (full Docker stack) | Revert new slices, view controls, and the auth-handler filter line; migration columns may stay (unused) |

**Branch naming (created at `sdd-apply` time, not now):** `feat/budget-member-administration`
(PR1, base `main`) → `feat/budget-member-administration-pr2a` (base PR1 branch) →
`feat/budget-member-administration-pr2b` (base PR2a branch) →
`feat/budget-member-administration-pr3` (base PR2b branch), following this repo's existing
`-pr1/-pr2a/-pr2b/-pr3` chained-branch convention (see `budget-line-redesign`).

---

## PR 1 (WU0) — Correctness Guard, No Schema Change (~180 lines)

### Phase 1: `BudgetRoleStrings` — single role-string convention

- [x] 1.1 RED: create `tests/MyBudget.Features.Tests/SharedKernel/Entities/BudgetRoleStringsTests.cs` — `ToApiString()` maps `ReadOnly → "read-only"`, `Owner/Admin/Operator → ToLowerInvariant()`; `TryParse` round-trips all 4 roles (`ToApiString ∘ TryParse == id`); `TryParse` returns `false` for unknown strings.
- [x] 1.2 GREEN: create `Project/src/MyBudget.Features/SharedKernel/Entities/BudgetRoleStrings.cs` with `ToApiString(this BudgetRole)` and `TryParse(string, out BudgetRole)` per design decision 3.
- [x] 1.3 REFACTOR: `dotnet test --filter BudgetRoleStrings`, confirm green.

### Phase 2: `GetCurrentUserHandler` hyphenated role fix (ME-1)

- [x] 2.1 RED: extend `tests/MyBudget.Integration.Tests/Features/Auth/LogoutAndMeTests.cs` — a user with a ReadOnly membership calling `GET /api/auth/me` gets `role: "read-only"` in `memberships[]`, not `"readonly"` (ME-1 scenario).
- [x] 2.2 GREEN: modify `GetCurrentUserHandler.cs:52` — replace `((BudgetRole)m.Role).ToString().ToLowerInvariant()` with `((BudgetRole)m.Role).ToApiString()`.
- [x] 2.3 REFACTOR: `dotnet test --filter LogoutAndMe`, confirm green.

### Phase 3: `AcceptInvitationHandler` duplicate-membership guard (ACCEPT-1)

- [x] 3.1 RED: extend `tests/MyBudget.Integration.Tests/Features/Auth/AcceptInvitationTests.cs` — user with an ACTIVE membership accepts a second valid token for the same budget → `409 Conflict`, error code `AUTH_ALREADY_MEMBER`, no `DbUpdateException`, pre-existing membership row untouched (invitation NOT marked used).
- [x] 3.2 RED: same file — happy-path response `role` is serialized via `BudgetRoleStrings.ToApiString()` (assert `"read-only"` for a ReadOnly-role invitation), not raw `.ToString()`.
- [x] 3.3 GREEN: modify `AcceptInvitationHandler.cs` — after step 5 (email match) and before `invitation.MarkUsed()`, add `_db.BudgetMemberships.FirstOrDefaultAsync(m => m.BudgetId == matched.BudgetId && m.UserId == cmd.UserId, ct)`; if found → `Result<AcceptInvitationResponse>.Failure("AUTH_ALREADY_MEMBER")` with no writes; switch the response's role to `((BudgetRole)matched.Role).ToApiString()` (design decisions 1–2).
- [x] 3.4 GREEN: modify `AcceptInvitationEndpoint.cs` — map `AUTH_ALREADY_MEMBER` to `Results.Problem(detail: "AUTH_ALREADY_MEMBER", statusCode: 409)`, matching the existing invitation-error mapping pattern.
- [x] 3.5 REFACTOR: `dotnet test --filter AcceptInvitation`, confirm 3.1–3.2 green plus all pre-existing `AcceptInvitationTests` cases still pass.

### Phase 4: `InviteUserToBudgetEndpoint` delegates to `BudgetRoleStrings.TryParse`

- [x] 4.1 RED: extend `tests/MyBudget.Integration.Tests/Features/Budgets/InviteUserToBudgetTests.cs` — inviting with `role: "read-only"` still succeeds (regression guard for the refactor).
- [x] 4.2 GREEN: modify `InviteUserToBudgetEndpoint.cs`'s private `TryParseRole` to delegate to `BudgetRoleStrings.TryParse` instead of its own switch, per design decision 3.
- [x] 4.3 REFACTOR: `dotnet test --filter InviteUserToBudget`, confirm no regression.

### Phase 5: Frontend `AUTH_ALREADY_MEMBER` error branch + i18n

- [x] 5.1 RED: extend the frontend spec for `AcceptInvitationView.vue` — an `AUTH_ALREADY_MEMBER` API error renders the `invitation.accept.error.alreadyMember` i18n key, not the generic error message. (Actual test file: `Project/frontend/src/views/__tests__/AcceptInvitationView.test.ts` — this repo has no `features/auth/views` path.)
- [x] 5.2 GREEN: modify `Project/frontend/src/views/AcceptInvitationView.vue` — add the `AUTH_ALREADY_MEMBER` branch to the existing error-code switch. (Actual path differs from design's `features/auth/views/` — see 5.1 note.)
- [x] 5.3 RED: extend `Project/frontend/src/i18n/__tests__/locales.spec.ts` — assert `invitation.accept.error.alreadyMember` exists in both `en.json` and `es.json`.
- [x] 5.4 GREEN: add the key to `Project/frontend/src/i18n/locales/en.json` and `es.json` (per auth spec's new i18n key list).
- [x] 5.5 REFACTOR: `pnpm test -- AcceptInvitationView locales`, confirm green.

---

## PR 2a (WU1 backend) — `MemberActionPolicy` + List/UpdateRole Slices (~380 lines)

> Base: PR1 branch. No schema change, no `BudgetAuthorizationHandler` touch.

### Phase 1: `MemberActionPolicy` — pure permission matrix

- [x] 6.1 RED: create `tests/MyBudget.Features.Tests/SharedKernel/Auth/MemberActionPolicyTests.cs` — every actor role × target role × `newRole?` matrix cell from design's 5-rule table, asserted in rule order: (1) `targetUserId == actorId` → `MEMBERS_CANNOT_ACT_ON_SELF`; (2) `targetRole == Owner` → `MEMBERS_CANNOT_ACT_ON_OWNER`; (3) `actorRole == Admin && targetRole == Admin` → `MEMBERS_CANNOT_ACT_ON_ADMIN`; (4) `newRole == Owner` → `MEMBERS_CANNOT_PROMOTE_TO_OWNER`; (5) no matching row → `MEMBERS_NOT_FOUND`; plus every allowed combination returns `null`.
- [x] 6.2 RED: same file — self-check fires before all other checks (an Owner acting on themselves as the "Owner target" gets `MEMBERS_CANNOT_ACT_ON_SELF`, not `MEMBERS_CANNOT_ACT_ON_OWNER`).
- [x] 6.3 GREEN: create `Project/src/MyBudget.Features/SharedKernel/Auth/Authorization/MemberActionPolicy.cs` — `Evaluate(actorId, actorRole, targetUserId, targetRole, newRole?) → string? errorCode`, implementing the 5 checks in order. (Signature uses `BudgetRole? targetRole` so rule 5's "no membership row" is representable by the pure function itself, per the ambiguity flagged in the orchestrator prompt.)
- [x] 6.4 REFACTOR: `dotnet test --filter MemberActionPolicy`, confirm all matrix cells green. (29/29 passed.)

### Phase 2: `ListBudgetMembers` slice (MEMBERS-LIST-1, WU1 scope)

- [x] 7.1 RED: create `tests/MyBudget.Integration.Tests/Features/Budgets/ListBudgetMembersTests.cs` — Admin lists 3 active memberships including Owner, gets `200 OK` with `userId/name/email/role/joinedAt` per row (Active members listed scenario). (Actual shape per design's Contracts section: `userId/email/firstName/lastName/role/joinedAt`, no combined `name` field — matches WU0's `BudgetRoleStrings` convention.)
- [x] 7.2 RED: same file — caller with `operator` role gets `403` (Insufficient role scenario). (Asserted via status code only, matching the existing convention for plain `budget:admin` policy failures — see `RenameBudgetTests.OperatorRole_Returns403`; the `AUTH_INSUFFICIENT_ROLE` body is only emitted by handlers with a manual Dapper-based policy bypass, e.g. `RestoreBudget`, not by the standard `[Authorize("budget:admin")]` 403 path.)
- [x] 7.3 GREEN: create `Project/src/MyBudget.Features/Features/Budgets/ListBudgetMembers/{ListBudgetMembersQuery,ListBudgetMembersHandler,ListBudgetMembersEndpoint}.cs` — Dapper read slice (no Validator), `GET /api/budgets/{id}/members`, `budget:admin` policy.
- [x] 7.4 REFACTOR: `dotnet test --filter ListBudgetMembers`, confirm green. (2/2 passed.)

### Phase 3: `UpdateMemberRole` slice (MEMBERS-ROLE-1)

- [x] 8.1 RED: create `tests/MyBudget.Integration.Tests/Features/Budgets/UpdateMemberRoleTests.cs` — Owner promotes Operator→Admin: `200 OK`, role updated.
- [x] 8.2 RED: same file — Owner demotes Admin→Operator: `200 OK`.
- [x] 8.3 RED: same file — Admin changes ReadOnly→Operator: `200 OK`.
- [x] 8.4 RED: same file — Admin targets Admin: `403 MEMBERS_CANNOT_ACT_ON_ADMIN`, no change persisted.
- [x] 8.5 RED: same file — caller targets own `userId` (Owner or Admin): `403 MEMBERS_CANNOT_ACT_ON_SELF`, role unchanged.
- [x] 8.6 RED: same file — target is the Owner row: `403 MEMBERS_CANNOT_ACT_ON_OWNER`, Owner role unchanged.
- [x] 8.7 RED: same file — `role: "owner"` on any non-Owner target: `422 MEMBERS_CANNOT_PROMOTE_TO_OWNER`.
- [x] 8.8 RED: same file — unknown `userId` for the budget: `404 MEMBERS_NOT_FOUND`.
- [x] 8.9 RED: same file — cache-eviction-via-second-request pattern: after a successful role change, a second immediate request by the affected user against a `budget:*`-gated endpoint reflects the new role (not a stale cached one). (Warms the cache via a `budget:read`-gated call under the target's old role, promotes via the Owner, then asserts the target's immediate `budget:admin`-gated call succeeds.)
- [x] 8.10 GREEN: create `Project/src/MyBudget.Features/Features/Budgets/UpdateMemberRole/{UpdateMemberRoleCommand,UpdateMemberRoleHandler,UpdateMemberRoleEndpoint,UpdateMemberRoleValidator}.cs` — `PATCH /api/budgets/{id}/members/{userId}/role`, `budget:admin` policy; handler does one Dapper read for actor role + target row, calls `MemberActionPolicy.Evaluate`, applies via EF, evicts `budget-membership:{userId}:{budgetId}` before returning (MEM-SC-3). (Role mutation uses `_db.Entry(membership).Property(m => m.Role).CurrentValue = cmd.NewRole` — `BudgetMembership` has no public role-change method yet; that method [`ChangeRole`] is explicitly PR3/WU2 Phase 1 scope per task 14.2, so this handler intentionally does not touch `BudgetMembership.cs`.)
- [x] 8.11 REFACTOR: `dotnet test --filter UpdateMemberRole`, confirm all of 8.1–8.9 green. (9/9 passed.)

---

## PR 2b (WU1 frontend) — Members Tab, View, Route (~330 lines)

> Base: PR2a branch.

### Phase 1: `useRoleGate` — add `isOwner`

- [x] 9.1 RED: extend `Project/frontend/src/features/budget-structure/composables/__tests__/useRoleGate.spec.ts` — `isOwner` is `true` only when the resolved role is `owner`.
- [x] 9.2 GREEN: modify `Project/frontend/src/features/budget-structure/composables/useRoleGate.ts` — add `isOwner` computed, additive to existing `isAdmin`/etc.
- [x] 9.3 REFACTOR: `pnpm test -- useRoleGate`, confirm green.

### Phase 2: `budgetMembers.api.ts`

- [x] 10.1 RED: create `Project/frontend/src/features/budget-structure/__tests__/budgetMembers.api.spec.ts` — `listMembers(budgetId)` calls `GET /api/budgets/{id}/members`; `updateMemberRole(budgetId, userId, role)` calls `PATCH /api/budgets/{id}/members/{userId}/role` with `{ role }` body, `role` values round-trip as `admin|operator|read-only`.
- [x] 10.2 GREEN: create `Project/frontend/src/features/budget-structure/api/budgetMembers.api.ts` with `listMembers`/`updateMemberRole`.
- [x] 10.3 REFACTOR: `pnpm test -- budgetMembers.api`, confirm green.

### Phase 3: `BudgetMembersView.vue`

- [x] 11.1 RED: create `Project/frontend/src/features/budget-structure/views/__tests__/BudgetMembersView.spec.ts` — Owner row excluded entirely from the table (no role `<select>`, no action control ever rendered for it).
- [x] 11.2 RED: same file — caller's own row renders neither a role `<select>` nor a remove button.
- [x] 11.3 RED: same file — Admin caller sees no controls on another Admin's row.
- [x] 11.4 RED: same file — `canActOn(m)` truth table: not admin → false; self → false; Owner target → false; non-owner caller + Admin target → false; otherwise → true (mirrors design's `canActOn` contract).
- [x] 11.5 RED: same file — role `<select>` reads and writes `read-only` (not `readonly`), matching `BudgetRoleStrings`.
- [x] 11.6 GREEN: create `Project/frontend/src/features/budget-structure/views/BudgetMembersView.vue` — renders `listMembers` rows, local `canActOn(m)` per design's Interfaces/Contracts snippet, calls `updateMemberRole` on select change, reuses `useRoleGate(budgetId).isAdmin`/`isOwner`.
- [x] 11.7 REFACTOR: `pnpm test -- BudgetMembersView`, confirm 11.1–11.5 green.

### Phase 4: Members tab, route, i18n

- [x] 12.1 RED: extend `Project/frontend/src/features/budget-structure/__tests__/BudgetTabs.spec.ts` — Members tab visible to Owner and Admin, positioned as the last tab, after "Dashboard"; hidden entirely from the DOM (not just disabled) for Operator and ReadOnly; has the active CSS class on the `BudgetMembers` route.
- [x] 12.2 GREEN: modify `Project/frontend/src/features/budget-structure/components/BudgetTabs.vue` — add the Members tab mirroring the Dashboard `RouterLink` + `isActive()` block, `MEMBERS_ROUTE_NAMES` + union entry, `v-if="isAdmin"`.
- [x] 12.2b CORRECTION (post-apply, user-directed): initial apply satisfied "immediately after Dashboard, before Cycles" literally by moving Dashboard from last to first tab position — a visible change for every user, not just admins. User reviewed and preferred minimal diff: reverted Dashboard to its original (last) position, Members now appended after it as the final tab. `specs/budget-structure-ui/spec.md` REQ-NAV-1 and its scenario updated to match; `BudgetTabs.spec.ts` position assertion rewritten (`membersIndex === labels.length - 1`) plus a new non-admin regression test asserting Dashboard stays last.
- [x] 12.3 GREEN: modify `Project/frontend/src/router/index.ts` — add `path: 'members'`, `name: 'BudgetMembers'` under `/budgets/:budgetId`, lazy-loaded `BudgetMembersView.vue`, `requiresAuth` only (no per-role guard, per design decision 11).
- [x] 12.4 RED: extend `locales.spec.ts` — assert `budgetStructure.members.*` keys (title, columns, actions, confirmations) exist in `en.json` and `es.json`.
- [x] 12.5 GREEN: add the `budgetStructure.members.*` keys to `en.json`/`es.json`.
- [x] 12.6 REFACTOR: `pnpm test -- BudgetTabs locales`, confirm green.

### Phase 5: E2E — open tab, demote (WU1 slice)

- [x] 13.1 RED: create `Project/frontend/e2e/budget-structure/budget-structure-members.spec.ts` — Owner opens the Members tab from a budget, sees the member list; demotes an Admin to Operator via the role select, sees it reflected after refresh.
- [x] 13.2 GREEN: executed by user against the full Docker/E2E stack — full Playwright suite (129 tests, including this spec) passed.
- [x] 13.3 REFACTOR: confirmed green by user, alongside the full UI/unit/integration layers.

---

## PR 3 (WU2) — Revoke & Restore Access, Security-Critical (~480 lines)

> Base: PR2b branch. **Do not fold into PR2a/PR2b** — isolates the `BudgetAuthorizationHandler`
> hot-path edit in its own reviewable diff (design's explicit instruction).

### Phase 1: `BudgetMembership` soft-delete + `ChangeRole`

- [x] 14.1 RED: extend the entity's unit test file (mirror `Budget.cs`'s existing soft-delete tests) — `SoftDelete()` sets `IsDeleted = true`, `DeletedAt = now`, bumps `UpdatedAt`; `Restore()` clears both, bumps `UpdatedAt`, leaves `JoinedAt` untouched; `ChangeRole(BudgetRole)` updates `Role`, bumps `UpdatedAt`.
- [x] 14.2 GREEN: modify `Project/src/MyBudget.Features/SharedKernel/Entities/BudgetMembership.cs` — add `IsDeleted`, `DeletedAt`, `SoftDelete()`, `Restore()` (copied verbatim from `Budget.cs`'s shape) plus a separate `ChangeRole(BudgetRole)` (design decision 8).
- [x] 14.3 REFACTOR: `dotnet test --filter BudgetMembership`, confirm green.
- [x] 14.4 REFACTOR (retrofit, flagged by `sdd-verify` on PR2a): `Project/src/MyBudget.Features/Features/Budgets/UpdateMemberRole/UpdateMemberRoleHandler.cs` currently mutates the role via `_db.Entry(membership).Property(m => m.Role).CurrentValue = cmd.NewRole` (raw EF change-tracker write, since `ChangeRole()` didn't exist yet in PR2a) — this silently skips the `UpdatedAt` bump that every other mutating handler gets via its domain method (`Rename`/`SoftDelete`/`Restore`). Now that `ChangeRole(BudgetRole)` exists (task 14.2), replace the raw EF write with `membership.ChangeRole(cmd.NewRole)`, matching `RenameBudgetHandler`'s `budget.Rename(...)` convention. Extend `UpdateMemberRoleTests.cs` with an assertion that `UpdatedAt` advances on a successful role change (RED first, then GREEN).

### Phase 2: EF migration

- [x] 15.1 GREEN: generate `Project/src/MyBudget.Features/Migrations/*_AddBudgetMembershipSoftDelete.cs` — `IsDeleted boolean NOT NULL DEFAULT false`, `DeletedAt timestamptz NULL`; confirm no change to `IX_BudgetMemberships_BudgetId_UserId` (stays a **total** unique index, per design decision 7 — a partial index would let accept insert a second row instead of restoring in place).
- [x] 15.2 Apply the migration locally and confirm existing seeded memberships default to `IsDeleted = false` with no data loss.

### Phase 3: `RemoveBudgetMember` slice (MEMBERS-REMOVE-1, security-critical)

- [x] 16.1 RED: create `tests/MyBudget.Integration.Tests/Features/Budgets/RemoveBudgetMemberTests.cs` — Owner removes an Operator: `204`, `IsDeleted=true`, `DeletedAt` set, cache evicted.
- [x] 16.2 RED: same file — Admin removes a ReadOnly member: `204`.
- [x] 16.3 RED: same file — Admin targets Admin: `403 MEMBERS_CANNOT_ACT_ON_ADMIN`, membership untouched.
- [x] 16.4 RED: same file — self-removal: `403 MEMBERS_CANNOT_ACT_ON_SELF`.
- [x] 16.5 RED: same file — target is Owner row: `403 MEMBERS_CANNOT_ACT_ON_OWNER`, untouched.
- [x] 16.6 RED: same file — **security-critical**: an Operator with a role cached moments earlier is removed, then immediately calls `GET /budgets/{id}/cycles` → `403 AUTH_NOT_A_MEMBER` in the same test run, not after TTL (proves synchronous eviction, not just eventual expiry).
- [x] 16.7 RED: same file — already-soft-deleted target: `404 MEMBERS_NOT_FOUND` (soft-deleted rows don't resolve as removal targets).
- [x] 16.8 GREEN: create `Project/src/MyBudget.Features/Features/Budgets/RemoveBudgetMember/{RemoveBudgetMemberCommand,RemoveBudgetMemberHandler,RemoveBudgetMemberEndpoint}.cs` — `DELETE /api/budgets/{id}/members/{userId}`, `budget:admin` + `MemberActionPolicy`; soft-delete only, never hard-delete; evict cache before returning.
- [x] 16.9 REFACTOR: `dotnet test --filter RemoveBudgetMember`, confirm all of 16.1–16.7 green.

### Phase 4: `RestoreBudgetMember` slice (MEMBERS-RESTORE-1, security-critical)

- [x] 17.1 RED: create `tests/MyBudget.Integration.Tests/Features/Budgets/RestoreBudgetMemberTests.cs` — Owner restores a soft-deleted Operator: `200`, `IsDeleted=false`, `DeletedAt=null`, role UNCHANGED from before removal, cache evicted.
- [x] 17.2 RED: same file — Admin restores a soft-deleted ReadOnly member: `200`, role unchanged.
- [x] 17.3 RED: same file — Admin restoring a soft-deleted Admin: `403 MEMBERS_CANNOT_ACT_ON_ADMIN`.
- [x] 17.4 RED: same file — target already active (`IsDeleted=false`): `409 MEMBERS_NOT_DELETED`.
- [x] 17.5 GREEN: create `Project/src/MyBudget.Features/Features/Budgets/RestoreBudgetMember/{RestoreBudgetMemberCommand,RestoreBudgetMemberHandler,RestoreBudgetMemberEndpoint}.cs` — `POST /api/budgets/{id}/members/{userId}/restore`, **standard `budget:admin` policy** (NOT `RestoreBudget`'s manual Dapper bypass). Add a code comment explaining why: `RestoreBudget` bypasses because the *actor's own* budget is soft-deleted (their `budget:admin` resolution would 404 through the auth handler's `IsDeleted=false` JOIN); here the *target member's* row is deleted, not the actor's, so the actor (an active Owner/Admin on a live budget) resolves normally — copying the bypass would drop a working gate for no reason (design decision 5). Role is NOT changed by this endpoint (spec MEMBERS-RESTORE-1 note).
- [x] 17.6 REFACTOR: `dotnet test --filter RestoreBudgetMember`, confirm all of 17.1–17.4 green.

### Phase 5: `BudgetAuthorizationHandler` filter — hot path (AUTHZ-1)

- [x] 18.1 RED: extend `tests/MyBudget.Features.Tests/SharedKernel/Auth/BudgetAuthorizationHandlerTests.cs` — a user whose only `BudgetMembership` row has `IsDeleted=true` is treated identically to no-membership: `403 AUTH_NOT_A_MEMBER`.
- [x] 18.2 RED: same file — a restored membership (`IsDeleted=false` again) authorizes normally on the next request.
- [x] 18.3 RED: same file — regression guard: a user with an ACTIVE membership at each of `owner/admin/operator/read-only` still resolves exactly as before this change (no new `403` introduced for active memberships).
- [x] 18.4 GREEN: modify `Project/src/MyBudget.Features/SharedKernel/Auth/Authorization/BudgetAuthorizationHandler.cs` — add exactly `AND bm."IsDeleted" = false` to the Dapper fallback WHERE clause (design decision 6); confirm the cache key/TTL, cache-hit fast path, `budget-not-found` 404-vs-403 disambiguation, and role-hierarchy `>=` comparison are all otherwise unchanged.
- [x] 18.5 REFACTOR: `dotnet test --filter BudgetAuthorizationHandler`, confirm 18.1–18.3 green.
- [x] 18.6 RED: **explicit regression sweep** — extend integration tests for one representative endpoint per existing policy tier with an ACTIVE membership, confirming unchanged behavior post-edit: `ListCycles` (`budget:read`/operator+), `CreateCycle` (`budget:admin`), `DeleteBudget`/`RestoreBudget` (`budget:owner`), `ListBudgetLines` (`budget:read`).
- [x] 18.7 GREEN: fix any regression surfaced by 18.6 (none expected — the filter only affects soft-deleted rows).
- [x] 18.8 REFACTOR: run the full existing integration suite (`dotnet test`), confirm zero regressions outside this change's files.

### Phase 6: Extend the WU0 guard — restore instead of insert (ACCEPT-1, WU2 extension)

- [x] 19.1 RED: extend `AcceptInvitationTests.cs` — user has a soft-deleted membership with `Role = operator`; a new invitation for the same email/budget has `Role = read-only`; accepting it returns `200`, the EXISTING row is restored (`IsDeleted=false`, `DeletedAt=null`) with `Role` updated to `read-only` (the invitation's role, not the pre-removal role); exactly one `BudgetMembership` row exists for `(BudgetId, UserId)` afterward — no insert.
- [x] 19.2 RED: same file — full flow: revoke a member → re-invite them → they accept → `200` with the NEW role (end-to-end ACCEPT-1 + MEMBERS-REMOVE-1 integration).
- [x] 19.3 GREEN: modify `AcceptInvitationHandler.cs` — branch on the existing-membership lookup from PR1 Phase 3: `!existing.IsDeleted` → `AUTH_ALREADY_MEMBER` (unchanged); `existing.IsDeleted` → `existing.Restore(); existing.ChangeRole((BudgetRole)matched.Role);` instead of insert; `existing is null` → insert (unchanged), per design's Interfaces/Contracts code snippet.
- [x] 19.4 REFACTOR: `dotnet test --filter AcceptInvitation`, confirm 19.1–19.2 green plus all pre-existing and PR1 cases still pass.

### Phase 7: Cache eviction audit across all new mutating handlers

- [x] 20.1 Confirm (via the RED tests already written in 8.9, 16.6, 17.1–17.2, 19.2) that `UpdateMemberRoleHandler`, `RemoveBudgetMemberHandler`, `RestoreBudgetMemberHandler`, and the extended `AcceptInvitationHandler` all call `_cache.Remove($"budget-membership:{userId}:{budgetId}")` for the affected member before returning — no handler is missing eviction (MEM-SC-3).

### Phase 8: Frontend — show-deleted toggle, restore action

- [x] 21.1 RED: extend `budgetMembers.api.spec.ts` — `listMembers(budgetId, { includeDeleted: true })` calls `GET /api/budgets/{id}/members?includeDeleted=true`; `removeMember(budgetId, userId)` calls `DELETE .../members/{userId}`; `restoreMember(budgetId, userId)` calls `POST .../members/{userId}/restore`.
- [x] 21.2 GREEN: extend `budgetMembers.api.ts` with `removeMember`/`restoreMember` and the `includeDeleted` param.
- [x] 21.3 RED: extend `BudgetMembersView.spec.ts` — "show deleted" toggle is visible and functional for Admin (not Owner-only); toggling ON re-fetches with `includeDeleted=true` and renders soft-deleted rows dimmed with a Restore button per row, gated by the same `canActOn` matrix; toggling OFF hides them again.
- [x] 21.4 RED: same file — clicking Remove on an actionable row calls `removeMember` then refetches; clicking Restore on a soft-deleted row calls `restoreMember` then refetches; `actionInProgress` disables the acted-on row's controls during the in-flight call (pattern from `BudgetSelectionView.vue`).
- [x] 21.5 GREEN: extend `BudgetMembersView.vue` — `showDeleted` ref, confirm-dialog before remove, `actionInProgress` per-row state, Restore button rendering (copied from `BudgetSelectionView.vue`'s established pattern).
- [x] 21.6 RED: extend `locales.spec.ts` — new `budgetStructure.members.{showDeleted,restore,removeConfirm,removeSuccess,restoreSuccess}` keys present in `en.json`/`es.json`.
- [x] 21.7 GREEN: add the keys to both locale files.
- [x] 21.8 REFACTOR: `pnpm test -- BudgetMembersView budgetMembers.api locales`, confirm 21.1, 21.3, 21.4, 21.6 all green.

### Phase 9: E2E — revoke, show-deleted, restore (extends PR2b's spec)

- [x] 22.1 RED: extend `budget-structure-members.spec.ts` — Owner revokes a member (confirm dialog → Delete), the row disappears from the default view; toggling "show deleted" reveals it dimmed; clicking Restore brings it back to the active list.
- [x] 22.2 RED: same file — a revoked member's session immediately loses access to a `budget:*` page (e.g. navigating to Cycles returns a 403/redirect), proving the cache-eviction contract end-to-end in the browser.
- [x] 22.3 GREEN: fix any implementation gap surfaced only under real browser timing/routing.
- [x] 22.4 REFACTOR: executed by user against the full Docker/E2E stack. Found one real bug on first run: `page.getByLabelText(...)` used in the spec (Testing-Library API, not a real Playwright method) — fixed to `page.getByLabel(...)`. After the fix, full spec file passes as part of the full suite (see 23.2).

### Phase 10: Full-suite regression + sign-off

- [x] 23.1 REFACTOR: run `dotnet test` (full backend suite), `pnpm test`, `pnpm build`, `pnpm lint` — confirm zero regressions outside this change's files.
- [x] 23.2 REFACTOR: executed by user against the full Docker stack. First run: 127 passed, 4 failed — 1 real bug (22.4's `getByLabelText`/`getByLabel` mixup) plus 3 failures in unrelated feature areas (budget-execution, budget-management multi-budget restore, budget-matrix closed-period). Investigated before assuming regression: those 3 specs re-run in isolation (fresh `globalSetup`/DB reset each) all passed clean, confirming they were sequential-run state pollution in the shared `mybudget_e2e` DB, not caused by the `BudgetAuthorizationHandler` edit. After the `getByLabel` fix, full suite re-run: **131 passed, 0 failed**. Zero regressions confirmed in other features' E2E flows.
- [x] 23.3 Update the Success Criteria checkboxes in `proposal.md` with evidence (test counts, actual changed-line totals per PR vs. this file's forecast).
