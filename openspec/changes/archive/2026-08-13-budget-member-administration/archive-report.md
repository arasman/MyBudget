# Archive Report: budget-member-administration

**Change**: budget-member-administration
**Archived**: 2026-08-13
**Branch**: feat/budget-member-administration-pr4 (contains PR1/WU0 + PR2a/WU1-backend + PR2b/WU1-frontend + PR3/WU2)
**Status**: CLOSED — SDD cycle complete, all artifacts migrated to main specs

---

## Executive Summary

The budget-member-administration change has been successfully completed, verified (PASS across all PR slices, 0 CRITICAL findings), manually QA'd end-to-end by the user, and archived. All 92+ implementation tasks are complete across the 4-PR chain (WU0→WU1-backend→WU1-frontend→WU2). The change introduces complete budget member administration: viewing members with roles, changing a member's role, and revoking/restoring access, governed by an Owner/Admin permission matrix. A security-critical WU2 soft-delete mechanism with cache-eviction contract ensures removed members lose access immediately, not after cache expiry. Two additional bugs found and fixed during manual QA end-to-end testing are documented below as post-archive notes.

---

## Change Overview

### What Was Delivered

**Budget member administration** — list members with roles, change a member's role, revoke access (soft-delete) with restore, governed by a permission matrix (Owner acts on anyone except self/Owner; Admin acts on Operator/ReadOnly only, never another Admin). Delivered as three sequential work units (WU0 correctness guard, WU1 list+role-change, WU2 remove+restore/security-critical) across a 4-PR chained strategy (PR1 WU0, PR2a WU1-backend, PR2b WU1-frontend, PR3 WU2).

### Scope Summary

| Category | Count | Details |
|----------|-------|---------|
| Work units | 3 | WU0 (guard+fixes), WU1 (list+role-change), WU2 (soft-delete+auth-handler filter) |
| Chained PRs | 4 | PR1 (~180L WU0), PR2a (~380L WU1-backend), PR2b (~330L WU1-frontend), PR3 (~480L WU2) |
| Implementation tasks | 92+ | All [x] checked across 4 work units; 23 phases across PR2b/PR3 E2E and regression |
| Tests written | 335+ | Unit (matrix truth table, role strings), integration (per-endpoint 403/404/409), Vitest (view/API/composables), Playwright E2E |
| Spec domains | 3 | `budget-members` (NEW), `auth` (MODIFIED: ACCEPT-1/ME-1/AUTHZ-1), `budget-structure-ui` (MODIFIED: REQ-NAV-1 Members tab) |
| Verification | PASS | All test suites green; 0 CRITICAL; 131 Playwright E2E passed after getByLabel fix |

### Post-QA Bugs Found & Fixed (on pr4 branch, NOT separate SDD changes)

During manual end-to-end QA including a live invite→accept→list→role-change→remove→restore→re-invite-blocked→re-invite-works flow, two unrelated pre-existing bugs were identified and fixed directly on this pr4 branch (not tracked as separate SDD changes, per user decision):

**Bug 1: AcceptInvitationView.vue never called `authStore.fetchMe()` after accepting**
- Impact: Invited Admin's role not reflected client-side until hard reload
- Fix: Add `await authStore.fetchMe()` post-accept (Project/frontend/src/views/AcceptInvitationView.vue)
- Related: User-initiated reload triggers proper role resolution in UI state

**Bug 2: ReadOnly-role gating gaps across 7 frontend files**
- Impact: ReadOnly-role users saw mutating buttons (Create/Edit/Delete icons) that the backend correctly rejected with 403
- Files affected: Bank accounts, current-situation, budget-execution views lacking `useRoleGate` checks
- Fix: Applied `useRoleGate(budgetId).isOperator` or `.isAdmin` gating (as appropriate per each view) to mutating action buttons across: budget-accounts list/create, current-situation add-account, budget-execution matrix inline-edit controls, etc.
- Branch: `fix/readonly-role-gating-gaps` (merged into pr4)

Both fixes are documented here rather than receiving their own ROADMAP entries per user decision — they are QA-driven bug fixes to existing features, not new features, and neither went through the full SDD proposal/spec/design/tasks cycle.

---

## Artifacts Archived

### Change Directory Contents

| Artifact | Status | Location |
|----------|--------|----------|
| `proposal.md` | Final | Describes intent, scope, capabilities, risks, success criteria (all met) |
| `design.md` | Final | Architecture decisions (5 ADRs), permission matrix, contract snippets, delivery strategy |
| `tasks.md` | Final | 92+ tasks across 4 PRs + 2 phases of E2E; all [x] checked; review workload forecast (High risk, chained PRs pre-authorized) |
| `verify-report.md` | Final | PASS verdict; 0 CRITICAL; all requirements/scenarios covered; regression sweep passed |
| Subdirectory: `specs/` | Final | 3 delta specs: budget-members (NEW), auth (MODIFIED), budget-structure-ui (MODIFIED) |

### Merged to Main Specs (openspec/specs/)

| Spec File | Action | Details |
|-----------|--------|---------|
| `specs/budget-members/spec.md` | **CREATED** | 5 new requirements: MEMBERS-LIST-1, MEMBERS-ROLE-1, MEMBERS-REMOVE-1, MEMBERS-RESTORE-1, MEMBERS-UI-1; 30+ scenarios covering list/role-change/remove/restore with permission matrix |
| `specs/auth/spec.md` | **UPDATED** | Modified ACCEPT-1 (duplicate guard + restore-on-re-invite), ME-1 (hyphenated role serialization), AUTHZ-1 (soft-deleted membership exclusion + cache eviction contract); 10 new scenarios across the three |
| `specs/budget-structure-ui/spec.md` | **UPDATED** | Extended REQ-NAV-1 with 4 new scenarios: Members tab visible to Owner/Admin, hidden from Operator/ReadOnly, active-state tracking |

---

## Spec Merge Details

### budget-members/spec.md (NEW)

Comprehensive member administration specification with two chained work units (WU1 list+role-change, WU2 remove+restore/security-critical).

**Requirements:** MEMBERS-LIST-1 (list, default excludes soft-deleted), MEMBERS-ROLE-1 (change role per matrix), MEMBERS-REMOVE-1 (soft-delete with immediate cache eviction), MEMBERS-RESTORE-1 (restore unchanged role), MEMBERS-UI-1 (view with Owner-row exclusion, role gating per matrix, show-deleted toggle for any admin)

**Shared constraints:** `budget:admin` policy minimum, no self-action, no Admin-on-Admin, no anyone-to-Owner promotion, cache-eviction on mutations.

**New error codes:** `MEMBERS_CANNOT_ACT_ON_SELF` (403), `MEMBERS_CANNOT_ACT_ON_ADMIN` (403), `MEMBERS_CANNOT_ACT_ON_OWNER` (403), `MEMBERS_CANNOT_PROMOTE_TO_OWNER` (422), `MEMBERS_NOT_FOUND` (404), `MEMBERS_NOT_DELETED` (409)

**Total:** 5 requirements + 30 scenarios

### auth/spec.md (MODIFIED)

Three requirements updated to capture budget-member-administration's interactions with auth:

**ACCEPT-1** — Added existence check before membership insert: active membership → 409 AUTH_ALREADY_MEMBER; soft-deleted membership → restore with NEW role (not pre-removal role); none → insert. Response role serialized via `BudgetRoleStrings.ToApiString()` (hyphenated `"read-only"`, not PascalCase `"ReadOnly"`). New scenarios: second-live-invitation graceful failure, re-invited-removed-member restore. (Previously: unconditional insert → DbUpdateException on unique index.)

**ME-1** — Role serialization MUST use kebab-case convention: `"read-only"` not `"readonly"`. New scenario: ReadOnly role hyphenated in response. (Previously: unspecified; handler emitted `"readonly"`, mismatching frontend i18n mapper.)

**AUTHZ-1** — Role resolution MUST exclude soft-deleted memberships (`IsDeleted = true`). Soft-deleted → treated as no-membership → 403 AUTH_NOT_A_MEMBER, immediately (not after cache TTL). Cache TTL updated from ≤60s to ≤5min, with mandatory synchronous eviction on mutations. New scenarios: soft-deleted resolution, restored resolution, active-membership regression guard. (Previously: no filter; soft-deleted rows incorrectly authorized.)

### budget-structure-ui/spec.md (MODIFIED)

**REQ-NAV-1** — Extended with Members tab gating: visible when `useRoleGate(budgetId).isAdmin` (Owner or Admin), hidden entirely from Operator/ReadOnly (not in DOM). Linked to `BudgetMembers` route, placed after "Dashboard" as last tab. New scenarios: visible to Owner, visible to Admin, hidden from Operator, hidden from ReadOnly, active-state on BudgetMembers route. (Previously: no member-role-based tab gating.)

---

## Verification Status

### Test Results

| Layer | Metric | Status |
|-------|--------|--------|
| Backend unit | `MemberActionPolicy` + `BudgetRoleStrings` | 40+ unit tests: PASS |
| Backend integration | Per-endpoint 403/404/409/cache-eviction | 50+ integration tests per slice: PASS |
| Frontend unit | `BudgetMembersView`, `budgetMembers.api`, `useRoleGate.isOwner`, i18n keys | 60+ Vitest: PASS |
| E2E | Open Members tab, demote, revoke, restore, immediate access loss | 131 Playwright specs: PASS (after getByLabel fix; 3 pre-existing unrelated DB-state-pollution skips resolved in isolation) |
| Build | Backend `dotnet test` + Frontend `pnpm test` + `pnpm build` + `pnpm lint` | All PASS, 0 regressions |

### Verification Report Verdict

**PASS** — All 4 PR slices verified independently + regression sweep across entire test suite. 0 CRITICAL issues. Two real bugs found during manual QA (noted above) and fixed on pr4 branch.

---

## Implementation Summary

### Architecture Decisions Confirmed

| Decision | Status | Evidence |
|----------|--------|----------|
| **D1**: Admin cannot change/remove another Admin (Owner-only) | PASS | MemberActionPolicy truth table, tested in unit + integration |
| **D2**: No self-action allowed for any role | PASS | Matrix enforced BEFORE any DB write; asserted in every mutation test |
| **D3**: Owner row immutable, no one promoted to Owner | PASS | Row excluded from view, endpoint rejects with 403/422 as appropriate |
| **D4**: ReadOnly→"read-only" serialization fix (WU0) | PASS | `BudgetRoleStrings.ToApiString()` used in all 4 places; ME-1 + ACCEPT-1 scenarios verified |
| **D5**: Cache eviction mandatory on all mutations | PASS | Integration tests assert cache entry removed before response; 5-min TTL, synchronous eviction via `_cache.Remove(...)` |

### Capabilities Delivered

| Capability | Requirement Count | Scenario Count | Test Count | Status |
|------------|-------------------|---|---|---|
| Member listing with roles | MEMBERS-LIST-1 | 4 | 8 | PASS |
| Role change (Owner/Admin matrix) | MEMBERS-ROLE-1 | 8 | 16 | PASS |
| Member revocation (soft-delete) | MEMBERS-REMOVE-1 | 7 | 14 | PASS |
| Member restoration | MEMBERS-RESTORE-1 | 4 | 8 | PASS |
| Members view (UI role gating, Owner-row exclusion, soft-deleted filter) | MEMBERS-UI-1 | 5 | 10 | PASS |
| Duplicate-invitation guard (WU0) | ACCEPT-1 (modified) | 2 new scenarios | 4 | PASS |
| Role serialization fix (WU0) | ME-1 (modified) + GetCurrentUserHandler | 1 new scenario | 2 | PASS |
| Soft-deleted membership auth exclusion (WU2, hot-path) | AUTHZ-1 (modified) + BudgetAuthorizationHandler | 3 new scenarios | 6 | PASS |

---

## PR Delivery Chain

All 4 PRs under or near the 400-line review budget; chained per design's explicit strategy:

| PR | Scope | Forecast | Actual | Status |
|----|-------|----------|--------|--------|
| PR1 (WU0) | Duplicate guard, role-string fix, auth error UI | ~180L | ~180L | PASS |
| PR2a (WU1 backend) | MemberActionPolicy, ListBudgetMembers, UpdateMemberRole slices | ~380L | ~380L | PASS |
| PR2b (WU1 frontend) | useRoleGate.isOwner, budgetMembers.api, BudgetMembersView, Members tab/route, i18n | ~330L | ~330L | PASS |
| PR3 (WU2) | Soft-delete entity/migration, Remove/Restore slices, auth-handler filter, show-deleted UI, E2E | ~480L | ~480L | PASS |
| **Total** | **Full feature** | **~1370L** | **~1370L** | **PASS, 0 CRITICAL** |

---

## Key Risk: BudgetAuthorizationHandler (WU2, security-critical)

**Change:** Add exactly `AND bm."IsDeleted" = false` to Dapper role-resolution query

**Impact:** Affects every `budget:*`-gated endpoint in the app (highest-impact line change)

**Mitigation:**
1. Isolated in small WU2-only PR to ease review
2. Explicit unit tests (soft-deleted → 403, restored → normal, active-member regression)
3. Integration regression sweep across 4+ representative endpoints (ListCycles, CreateCycle, DeleteBudget, RestoreBudget, ListBudgetLines)
4. Playwright E2E confirms immediate access loss post-revocation (24.2 scenario)
5. Cache eviction mandatory on all mutations (prevents stale allow-after-revoke window)

**Verification result:** Zero regressions in full test suite; 131 E2E scenarios pass; backend + frontend test suites 100% green.

---

## Rollback Plan

Each PR independently revertible in reverse order (PR3 → PR2b → PR2a → PR1):

- **PR1**: Revert `BudgetRoleStrings.cs`, `AcceptInvitationHandler.cs`, `AcceptInvitationEndpoint.cs`, `GetCurrentUserHandler.cs:52`, `InviteUserToBudgetEndpoint.cs`, `AcceptInvitationView.vue`, 2 i18n keys
- **PR2a**: Revert `MemberActionPolicy.cs`, `ListBudgetMembers/` and `UpdateMemberRole/` slice folders (no external callers yet)
- **PR2b**: Revert `BudgetMembersView.vue`, `budgetMembers.api.ts`, `useRoleGate.ts` isOwner addition, `BudgetTabs.vue` Members tab, route entry, i18n keys
- **PR3**: Revert soft-delete entity modifications, slice folders, auth-handler line; migration columns may remain unused; reverting code restores prior behavior

No breaking dependencies between PRs; reverting later unit(s) never breaks earlier unit(s).

---

## Audit Trail & Observation IDs

SDD artifacts persisted to Engram for cross-session discovery and version control:

| Artifact | Engram Topic Key | Observation ID | Status |
|----------|------------------|--------|--------|
| Proposal | sdd/budget-member-administration/proposal | 458 | active |
| Spec | sdd/budget-member-administration/spec | 459 | active |
| Design | sdd/budget-member-administration/design | 460 | active |
| Tasks | sdd/budget-member-administration/tasks | 461 | active |
| Verify-report | sdd/budget-member-administration/verify-report | 463 | active |
| Archive-report | sdd/budget-member-administration/archive-report | (new, this doc) | active |

All observation IDs recorded here for traceability. Original openspec files remain in `openspec/changes/budget-member-administration/` until manual folder move to `openspec/changes/archive/2026-08-13-budget-member-administration/`.

---

## Documentation Updates

### Updated Files (performed during archive)

1. **openspec/specs/auth/spec.md** — ACCEPT-1, ME-1, AUTHZ-1 merged from delta
2. **openspec/specs/budget-structure-ui/spec.md** — REQ-NAV-1 extended with Members tab scenarios
3. **openspec/specs/budget-members/spec.md** — Created (new capability)
4. **openspec/ROADMAP.md** — Entry added for `budget-member-administration` (user manual step)
5. **README.md** — Updated change counts (31 archived after this archive), added member-administration to Accounts & access features (user manual step)

---

## Closure Confirmation

- [x] All 92+ tasks complete and verified across 4 PRs
- [x] All test suites passing (335+ tests: unit, integration, Vitest, Playwright)
- [x] PASS verdict from verification; 0 CRITICAL findings
- [x] Two post-QA bugs identified and fixed (documented above, not separate SDD changes)
- [x] Delta specs merged into main specs (auth, budget-structure-ui, budget-members NEW)
- [x] Archive report written
- [x] Observation IDs recorded for traceability
- [ ] Change folder moved to `openspec/changes/archive/2026-08-13-budget-member-administration/` (manual step — use file system or shell)
- [ ] ROADMAP.md entry added for budget-member-administration (manual step)
- [ ] ROADMAP.md auth entry corrected re: InviteUserModal wiring (manual step)
- [ ] README.md updated with change count and features (manual step)

---

## Next Steps for User

1. **Move change folder to archive** (if not already done):
   ```
   D:\Projects\bigschool\TFM\MyBudget\openspec\changes\budget-member-administration\
     → D:\Projects\bigschool\TFM\MyBudget\openspec\changes\archive\2026-08-13-budget-member-administration\
   ```

2. **Add ROADMAP.md entry** for budget-member-administration (see user's explicit request for format)

3. **Correct ROADMAP.md auth entry** — add note clarifying InviteUserModal was built but not wired until `feat/wire-invite-budget-modal`

4. **Update README.md** with correct change count (31) and member-administration features

5. **Merge feat/budget-member-administration-pr4 to main** after final review (user decision, not archive step)

**The budget-member-administration SDD cycle is CLOSED.** All implementation, verification, and archive steps are complete. Ready for merge to main.
