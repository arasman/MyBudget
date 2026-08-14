# Proposal: Budget Member Administration

## Intent

A budget can have several members today, but nothing in the product lets an owner or admin **see or manage** them. Members can only be created (invite → accept); after that the membership is invisible and immutable from the UI. Consequences today:

- No way to see who has access to a budget, or with which role.
- No way to correct a wrong role — the only fix is a support/DB action.
- No way to revoke access when someone leaves. Financial data stays exposed to a person who should no longer see it. This is the sharpest gap: a shared budget with no off-boarding.
- A real crash path: two live invitation tokens for the same email (already possible; guaranteed once resend-invite exists) both get clicked. The second click hits the unique index `IX_BudgetMemberships_BudgetId_UserId` and `SaveChangesAsync` throws an unhandled `DbUpdateException` — verified: no try/catch in `AcceptInvitationHandler`, no global exception middleware for it. The user sees a 500 on an otherwise valid invitation.
- A latent display bug that a member list would immediately expose: the server emits `"readonly"` while the whole client contract is `"read-only"`, so every ReadOnly row would render an untranslated i18n key.

This is **one cohesive change delivered as three sequential work units**, not three SDD changes — per explicit user decision to work them as a whole. The units are review slices, not independent products; a Members tab that lists members but cannot revoke access is not the outcome we are shipping.

## Scope

### In Scope

**WU0 — Correctness guard (backend only, no schema change)**
- `AcceptInvitationHandler`: before `_db.BudgetMemberships.Add(membership)`, check whether a membership already exists for `(BudgetId, UserId)`. If it does, return a graceful `Result.Failure` with a new error code instead of letting the unique index throw.
- `GetCurrentUserHandler.cs:52`: emit the hyphenated `"read-only"` form expected by `InviteUserToBudgetEndpoint.TryParseRole` and by the frontend `toRoleKey()` in `utils/enum-key.ts`. One-line fix.

**WU1 — Members tab: list + change role (no schema change, no auth-handler touch)**
- New "Members" tab in `BudgetTabs.vue`, placed after Dashboard, following the existing `RouterLink` + `isActive()` pattern exactly; new lazy-loaded child route under `/budgets/:budgetId`.
- `ListBudgetMembers` endpoint (`budget:admin`) — member name/email, role, join date.
- `UpdateMemberRole` endpoint (`budget:admin` + in-handler escalation check) and the UI role selector.
- Cache eviction of `budget-membership:{userId}:{budgetId}` for the affected member on every role change.

**WU2 — Revoke and restore access (schema change, security-critical)**
- `IsDeleted` / `DeletedAt` / `SoftDelete()` / `Restore()` on `BudgetMembership`, mirroring `Budget.cs`; one EF Core migration (existing rows default `IsDeleted = false`).
- `RemoveBudgetMember` and `RestoreBudgetMember` endpoints, plus a "show deleted" toggle and restore action mirroring the shipped `deleteBudget`/`restoreBudget` pattern in `budgets.api.ts` / `BudgetSelectionView.vue`.
- **`BudgetAuthorizationHandler`**: its Dapper role-resolution query MUST filter out soft-deleted memberships once the column exists. Highest-risk item in the change — shared hot path behind every `budget:*`-gated endpoint in the app.
- **Required extension of the WU0 guard**: once soft-deleted rows exist, "remove member → re-invite → accept" must **restore the existing row**, not insert a new one, or the unique index collides again. WU0's guard becomes: active membership → graceful failure; soft-deleted membership → restore with the invited role.

### Out of Scope

- **Full invitation lifecycle management** — deferred to a separate future SDD change: no `Invitation.Status` enum (Pending/Accepted/Expired/Revoked/Declined), no decline/reject endpoint, no resend-with-invalidation flow, no invitations sub-view or tab, no new `Invitation` fields or migration. WU0 is the only invitation-adjacent work here and is intentionally narrow.
- Ownership transfer, owner removal, or any mutation of the Owner row.
- A member-initiated "leave budget" flow (self-removal).
- Reattribution of financial data on removal — confirmed unnecessary: no `BudgetLine`/`CutRecord` field attributes records to a user.
- Reconciling `Budget.OwnerId` with the Owner-role membership row (they stay as they are today).

## Design Decisions (resolved here, open in exploration)

| # | Decision | Resolution | Rationale |
|---|---|---|---|
| D1 | Can an Admin change/remove another Admin? | **No — Owner-only.** Admin may act on Operator and ReadOnly members only; Owner may act on anyone but themselves. | Mirrors the existing `budget:owner` gate on DeleteBudget/RestoreBudget; prevents an admin coup and admin-vs-admin lockout races. |
| D2 | Self-action | **Forbidden for everyone, including Owner and Admin.** Enforced at both frontend gating and backend authorization. | Prevents self-lockout and privilege self-escalation; backend enforcement is required because frontend gating is not a security boundary. |
| D3 | Owner rows | **Excluded entirely** from role-change and remove in this UI; promotion *to* Owner is also rejected. | Matches `InviteUserToBudgetValidator`'s `NotEqual(BudgetRole.Owner)`; ownership transfer is a distinct, riskier feature. |
| D4 | Fix the `"readonly"` vs `"read-only"` serialization bug? | **Yes, in WU0.** | The Members list makes it immediately visible on every ReadOnly row; one-line fix; the new role selector must read and write the same string convention. |
| D5 | Endpoint policy tier | `budget:admin` on List/UpdateRole/Remove/Restore, **plus an in-handler check** rejecting any action whose *target* is Owner-or-Admin unless the caller is the Owner. | A single policy tier cannot express "admin, except against admins"; the manual-check precedent already exists in `RestoreBudget`. |

## Capabilities

### New Capabilities

- `budget-members`: list budget members, change a member's role, revoke access (soft delete), restore access; the permission matrix (D1–D3, D5), cache-eviction contract, and the Members tab/route/view.

### Modified Capabilities

- `auth`: `ACCEPT-1` (duplicate-membership guard; restore-instead-of-insert once soft delete exists), `ME-1` (role string serialized as `read-only`), `AUTHZ-1` (role resolution MUST ignore soft-deleted memberships).
- `budget-structure-ui`: `BudgetTabs` gains a Members tab, gated by `useRoleGate(budgetId).isAdmin`.

## Approach

Backend follows the existing `Features/Budgets/{Verb}` vertical-slice pattern (Command/Query, Endpoint, Handler, Validator). `RestoreBudgetMember` mirrors `RestoreBudget`'s manual Dapper role check, since a soft-deleted row cannot resolve through the standard handler. Frontend adds one API module (`budgetMembers.api.ts`), one view (`BudgetMembersView.vue`), and one tab, reusing `useRoleGate` rather than duplicating `BudgetSelectionView`'s local `canEdit()` helper; `showDeleted` / confirm-dialog / `actionInProgress` patterns are copied from `BudgetSelectionView`.

Delivery: WU0 → WU1 → WU2, in that order, as chained PRs (WU2 depends on WU0's guard and on WU1's view). Ordering is deliberate — WU0 lands the crash fix and the string fix before any UI makes them visible; WU2 isolates the migration and the `BudgetAuthorizationHandler` edit into a small, hot-path-tagged diff so the reviewer's attention is concentrated where the risk is.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `Features/Auth/AcceptInvitation/AcceptInvitationHandler.cs` | Modified | Existence guard; later, restore-instead-of-insert |
| `Features/Auth/GetCurrentUser/GetCurrentUserHandler.cs` | Modified | `read-only` serialization (line 52) |
| `Features/Budgets/ListBudgetMembers/`, `UpdateMemberRole/`, `RemoveBudgetMember/`, `RestoreBudgetMember/` | New | Four vertical slices |
| `SharedKernel/Entities/BudgetMembership.cs` | Modified | `IsDeleted`/`DeletedAt`/`SoftDelete()`/`Restore()`/`ChangeRole()` |
| `Migrations/*AddBudgetMembershipSoftDelete` | New | Additive columns, default `false` |
| `SharedKernel/Auth/Authorization/BudgetAuthorizationHandler.cs` | Modified | **Security-critical**: filter soft-deleted memberships |
| `frontend/src/features/budget-structure/components/BudgetTabs.vue`, `router/index.ts` | Modified | Members tab + route |
| `frontend/src/features/budget-structure/views/BudgetMembersView.vue`, `api/budgetMembers.api.ts` | New | View + API client |
| `frontend/src/i18n/locales/{en,es}.json` | Modified | `budgetStructure.members.*` keys, both locales |
| `tests/MyBudget.Integration.Tests/Features/Budgets/`, frontend `__tests__/`, `e2e/` | New | All four test layers |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| `BudgetAuthorizationHandler` change breaks every `budget:*`-gated endpoint | Med / **High impact** | Isolated in WU2; integration regression across existing gated endpoints; explicit hot-path review tag |
| Removed/demoted member keeps access for up to 5 min via `IMemoryCache` | High | Every mutation handler MUST evict `budget-membership:{userId}:{budgetId}`; asserted in integration tests |
| Unique index collision on remove → re-invite → accept | High | WU0 guard extended in WU2 to restore the soft-deleted row |
| Backend authorization weaker than frontend gating (D1/D2 enforced only in the UI) | Med | Negative integration tests per forbidden combination (self, Owner target, Admin→Admin) |
| Owner locks themselves out of administration | Low | D2 forbids self-action; Owner row is structurally unmanageable |
| Migration on deployed data | Low | Additive columns with `false` default; no backfill |
| Three work units exceed the 400-line review budget as one PR | High | Chained PRs, one per work unit; forecast confirmed at `sdd-tasks` |

## Rollback Plan

- **WU0**: revert two files. No schema, no contract change beyond a role string that already matches what the client expects.
- **WU1**: revert the tab, route, view, API module, i18n keys, and two backend slices. Purely additive.
- **WU2**: revert the endpoints, the view controls, and the `BudgetAuthorizationHandler` filter; the migration is additive, so the columns may be left in place (unused) rather than rolled back — safest order is code revert first, migration down only if required. Reverting `BudgetAuthorizationHandler` restores today's behaviour exactly.

Each unit is an independently revertible PR; reverting a later unit never breaks an earlier one.

## Dependencies

- New branch off `main` created **before implementation starts** (branch-before-cycle convention). Not created in this phase.
- Strict TDD: tests written first at all four layers (unit, integration, frontend, E2E).
- No external dependencies; no new packages.

## Success Criteria

- [x] Clicking a second live invitation token for an email that is already a member returns a graceful error, not a 500. (WU0, PR1 — `AUTH_ALREADY_MEMBER` 409, `AcceptInvitationTests.ActiveMembershipAlreadyExists_SecondValidToken_Returns409_NoWrites`.)
- [x] ReadOnly members render a correctly translated role label in EN and ES everywhere the role is shown. (WU0, PR1 — `BudgetRoleStrings.ToApiString()`.)
- [x] An owner or admin can open a Members tab for the active budget and see every current member with their role. (WU1, PR2a/PR2b — `ListBudgetMembers` + `BudgetMembersView.vue`.)
- [x] An owner can change any non-Owner member's role; an admin can change only Operator/ReadOnly members' roles; nobody can change their own; the Owner row offers no role control. (WU1, PR2a/PR2b — `MemberActionPolicy` + `UpdateMemberRole`.)
- [x] Revoking a member's access removes their ability to reach any `budget:*`-gated endpoint for that budget within one request — not after cache expiry. (WU2, PR3 — `RemoveBudgetMemberTests.RemovedMember_LosesAccessImmediately_NotAfterCacheTtl`, `BudgetAuthorizationTests.SoftDeletedMembership_ResolvesAsNoMembership_Returns403`.)
- [x] Revoked members are hidden by default, visible under "show deleted", and restorable. (WU2, PR3 — `ListBudgetMembers` `includeDeleted` param + `BudgetMembersView.vue` show-deleted toggle/Restore.)
- [x] A revoked member can be re-invited and accept successfully, without a unique-index error. (WU2, PR3 — `AcceptInvitationTests.RevokeThenReInviteThenAccept_FullFlow_Returns200WithNewRole`.)
- [x] All existing `budget:*`-gated endpoints keep working unchanged after the `BudgetAuthorizationHandler` edit. (WU2, PR3 — regression sweep: `ListCycles`/`ListBudgetLines` (`budget:read`), `CreateCycle` (`budget:admin`), `DeleteBudget`/`RestoreBudget` (`budget:owner`), all active-membership scenarios pass unchanged.)
- [x] All four test layers green; every new UI string present in `en.json` and `es.json`. (Unit + integration + frontend green with real evidence below; E2E written, execution pending the user/CI's Docker stack.)

### Evidence (as of PR3 completion, 2026-08-13)

| Layer | Command | Result |
|---|---|---|
| Backend unit | `dotnet test tests/MyBudget.Features.Tests -c Release` | 540/540 passed |
| Backend integration | `dotnet test tests/MyBudget.Integration.Tests -c Release` | 314/317 passed, 3 skipped (pre-existing `BudgetLineRevisionTests` concurrency tests, unrelated to this change), 0 failed |
| Frontend unit/component | `pnpm vitest run` | 785/785 passed, 89 files |
| Frontend build | `pnpm run build` (`vue-tsc -b && vite build`) | 0 TS errors, built in 1.95s |
| Frontend lint | `pnpm run lint` (`eslint src`) | exit 0 |
| E2E | `pnpm exec playwright test -- budget-structure-members` | Written (13.1 from PR2b + 22.1/22.2 from PR3), **not executed** — no Docker/E2E stack in this environment; requires the user/CI to run against the full stack |

**Actual changed-line totals per PR** (authored additions + deletions, excluding EF-generated `*.Designer.cs`/model-snapshot machinery per the review-workload guard's golden exclusion):

| PR | Forecast | Actual |
|---|---|---|
| PR1 (WU0) | ~150 | ~180 (per PR1 apply-progress) |
| PR2a (WU1 backend) | (part of ~380) | ~380 (per PR2a verify report, 528+290 tests) |
| PR2b (WU1 frontend) | (part of ~380) | ~330 (per PR2b verify report, 769 tests) |
| PR3 (WU2) | ~350–480 | **~1782** (948 insertions + 57 deletions across 22 modified tracked files, plus ~777 lines across 7 new untracked files — `RemoveBudgetMember`/`RestoreBudgetMember` slices, `BudgetMembershipDomainTests.cs`, `RemoveBudgetMemberTests.cs`, `RestoreBudgetMemberTests.cs`, and the hand-inspected additive migration; the auto-generated `*.Designer.cs` model snapshot, ~1354 lines, is excluded as a generated golden) |

PR3 significantly exceeds its own forecast — the gap versus the ~350–480 estimate is explained by: the WU2-required `ListBudgetMembers` `includeDeleted`/`isDeleted` extension (not itemized as its own PR3 task in `tasks.md` but required by `spec.md`'s MEMBERS-LIST-1 WU2 scenarios and the frontend show-deleted toggle), the security-critical regression-sweep tests explicitly requested for the `BudgetAuthorizationHandler` edit, and the full E2E spec extension. This was flagged during apply rather than silently absorbed; the user's launch instructions explicitly assigned the complete PR3 scope (all 10 phases) as one deliverable — "this is the full and final PR of this SDD change" — so no further chain-splitting was applied. Flagged here for `sdd-verify`/reviewer awareness given the review-workload guard's 400-line budget.

## Proposal Question Round (for user review before spec) — RESOLVED

The scope above is final per prior conversation. Three residual product questions were raised; all three are now resolved by explicit user decision, each confirming the assumed/recommended option. Kept below, unmodified, for audit trail.

1. **Members tab visibility** — should the tab be hidden entirely from Operator/ReadOnly members, or visible read-only (they see who else is on the budget, with no actions)? Assumed: hidden (gated by `isAdmin`), matching `budget:admin` on the list endpoint. Read-only visibility would require relaxing the list endpoint to `budget:read`.
   **Resolution: hidden entirely from Operator/ReadOnly.** Only Owner/Admin see the Members tab; `ListBudgetMembers` requires the `budget:admin` policy, not `budget:read`.
2. **Revoked-member visibility scope** — is "show deleted" available to any admin, or Owner-only? Assumed: any admin who can act on the member can also see them revoked.
   **Resolution: any admin (Owner + Admin).** The "show deleted" toggle sits at the same access tier as the remove/change-role actions; it is not Owner-only.
3. **Re-invite of a revoked member** — on accept, should the restored membership take the **role from the new invitation** (assumed) or the role it had before revocation?
   **Resolution: role from the new invitation.** The WU2 restore path (`AcceptInvitationHandler`, once the WU0 guard is extended) sets the restored membership's role from the invitation being accepted, not the pre-revocation role.
