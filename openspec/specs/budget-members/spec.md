# budget-members Specification

## Purpose

Budget member administration: list members, change a member's role, revoke (soft-delete) access,
and restore access, governed by an Owner/Admin permission matrix, a cache-eviction contract, and
the Members tab view. Delivered as two chained work units: WU1 (list + role change, no schema
change) and WU2 (remove + restore, schema change, security-critical). Requirement/scenario tags
below indicate the work unit that introduces each behavior.

## Shared Constraints

- MEM-SC-1: All endpoints below require the `budget:admin` policy at minimum.
- MEM-SC-2: No endpoint MAY allow (a) a caller acting on their own membership, (b) an Admin acting
  on another Admin or the Owner, (c) any actor acting on or promoting anyone to the Owner role.
  Enforced server-side via an in-handler check regardless of frontend gating.
- MEM-SC-3 (WU2): Every mutation (`UpdateMemberRole`, `RemoveBudgetMember`, `RestoreBudgetMember`)
  MUST evict `budget-membership:{userId}:{budgetId}` for the affected member before returning.

**New error codes:** `MEMBERS_CANNOT_ACT_ON_SELF` (403), `MEMBERS_CANNOT_ACT_ON_ADMIN` (403),
`MEMBERS_CANNOT_ACT_ON_OWNER` (403), `MEMBERS_CANNOT_PROMOTE_TO_OWNER` (422),
`MEMBERS_NOT_FOUND` (404), `MEMBERS_NOT_DELETED` (409).

## Requirements

### Requirement: MEMBERS-LIST-1 — List Budget Members (WU1, extended WU2)

The system MUST expose `GET /api/budgets/{budgetId}/members`, gated by `budget:admin`. Each row
MUST include `userId`, `name`, `email`, `role` (`owner`|`admin`|`operator`|`read-only`),
`joinedAt`, `isDeleted`. Default (no param, or `includeDeleted=false`) MUST exclude soft-deleted
rows. `includeDeleted=true` MUST include both.

#### Scenario: Active members listed (WU1)

- GIVEN budget X has 3 active memberships including the Owner
- WHEN `GET /members` is called with no query params by an Admin
- THEN `200 OK` returns 3 rows with role and joinedAt for each

#### Scenario: Insufficient role (WU1)

- GIVEN the caller has `operator` role
- WHEN `GET /members` is called
- THEN `403 Forbidden` is returned with error code `AUTH_INSUFFICIENT_ROLE`

#### Scenario: Default excludes soft-deleted (WU2)

- GIVEN budget X has 2 active and 1 soft-deleted membership
- WHEN `GET /members` is called with default params
- THEN `200 OK` returns only the 2 active rows

#### Scenario: includeDeleted=true includes soft-deleted (WU2)

- GIVEN the same setup as above
- WHEN `GET /members?includeDeleted=true` is called
- THEN `200 OK` returns all 3 rows, the soft-deleted one with `isDeleted: true`

---

### Requirement: MEMBERS-ROLE-1 — Change a Member's Role (WU1)

The system MUST expose `PATCH /api/budgets/{budgetId}/members/{userId}/role`, gated by
`budget:admin` plus an in-handler check implementing:

| Caller role | May change ReadOnly/Operator | May change Admin | May change Owner |
|---|---|---|---|
| Owner | Yes | Yes | No — never (row excluded) |
| Admin | Yes | No | No |

The new role MUST be one of `admin`, `operator`, `read-only` — never `owner`. The caller MUST NOT
target their own `userId`, regardless of role. On success, evict
`budget-membership:{targetUserId}:{budgetId}` before returning.

#### Scenario: Owner promotes Operator to Admin (WU1)

- GIVEN caller is Owner, target is Operator
- WHEN `PATCH .../role` is called with `role: "admin"`
- THEN `200 OK`, the membership role is updated, and the target's cache entry is evicted

#### Scenario: Owner demotes Admin to Operator (WU1)

- GIVEN caller is Owner, target is Admin
- WHEN `PATCH .../role` is called with `role: "operator"`
- THEN `200 OK` and the role is updated

#### Scenario: Admin changes a ReadOnly member's role (WU1)

- GIVEN caller is Admin, target is ReadOnly
- WHEN `PATCH .../role` is called with `role: "operator"`
- THEN `200 OK` and the role is updated

#### Scenario: Admin forbidden from changing another Admin (WU1)

- GIVEN caller is Admin, target is Admin
- WHEN `PATCH .../role` is called
- THEN `403 Forbidden` is returned with `MEMBERS_CANNOT_ACT_ON_ADMIN`; no change is persisted

#### Scenario: Self-role-change forbidden, any role (WU1)

- GIVEN the caller's own `userId` is the target (caller is Owner or Admin)
- WHEN `PATCH .../role` is called
- THEN `403 Forbidden` is returned with `MEMBERS_CANNOT_ACT_ON_SELF`; role unchanged

#### Scenario: Owner row unchangeable (WU1)

- GIVEN the target membership is the budget's Owner row
- WHEN `PATCH .../role` is called by any caller
- THEN `403 Forbidden` is returned with `MEMBERS_CANNOT_ACT_ON_OWNER`; Owner role unchanged

#### Scenario: Promotion to Owner rejected (WU1)

- GIVEN caller is Owner, target is any non-Owner member
- WHEN `PATCH .../role` is called with `role: "owner"`
- THEN `422 Unprocessable Entity` is returned with `MEMBERS_CANNOT_PROMOTE_TO_OWNER`

#### Scenario: Target has no membership (WU1)

- GIVEN `userId` has no `BudgetMembership` row in budget `{budgetId}`
- WHEN `PATCH .../role` is called
- THEN `404 Not Found` is returned with `MEMBERS_NOT_FOUND`

---

### Requirement: MEMBERS-REMOVE-1 — Revoke Member Access (WU2, security-critical)

The system MUST expose `DELETE /api/budgets/{budgetId}/members/{userId}`, gated by `budget:admin`
plus the same permission matrix as MEMBERS-ROLE-1. On success the membership MUST be soft-deleted
(`IsDeleted = true`, `DeletedAt = now`) — never hard-deleted. The system MUST evict
`budget-membership:{targetUserId}:{budgetId}` in the same request, before returning.

**Pre/post authorization contract:** immediately after this call returns, any subsequent request by
the removed user to ANY `budget:*`-gated endpoint for this budget MUST resolve to
`403 AUTH_NOT_A_MEMBER` — not after the cache's 5-minute TTL expires.

#### Scenario: Owner removes an Operator (WU2)

- GIVEN caller is Owner, target is Operator
- WHEN `DELETE .../members/{userId}` is called
- THEN `204 No Content`; membership `IsDeleted=true`, `DeletedAt` set; cache evicted

#### Scenario: Admin removes a ReadOnly member (WU2)

- GIVEN caller is Admin, target is ReadOnly
- WHEN `DELETE .../members/{userId}` is called
- THEN `204 No Content` and the membership is soft-deleted

#### Scenario: Admin forbidden from removing another Admin (WU2)

- GIVEN caller is Admin, target is Admin
- WHEN `DELETE .../members/{userId}` is called
- THEN `403 Forbidden` with `MEMBERS_CANNOT_ACT_ON_ADMIN`; membership untouched

#### Scenario: Self-removal forbidden (WU2)

- GIVEN the caller's own `userId` is the target
- WHEN `DELETE .../members/{userId}` is called
- THEN `403 Forbidden` with `MEMBERS_CANNOT_ACT_ON_SELF`

#### Scenario: Owner row cannot be removed (WU2)

- GIVEN the target is the budget's Owner row
- WHEN `DELETE .../members/{userId}` is called by any caller
- THEN `403 Forbidden` with `MEMBERS_CANNOT_ACT_ON_OWNER`; Owner membership untouched

#### Scenario: Removed member loses access immediately, not after cache TTL (WU2, security-critical)

- GIVEN an Operator whose role was cached from a request made moments earlier (within the
  5-minute cache TTL), then removed via this endpoint
- WHEN the removed user calls any `budget:*`-gated endpoint (e.g. `GET /budgets/{id}/cycles`)
  immediately after removal
- THEN `403 Forbidden` with `AUTH_NOT_A_MEMBER` is returned — the stale cache entry is not
  consulted because it was evicted synchronously during removal

#### Scenario: Already-removed member (WU2)

- GIVEN the target membership already has `IsDeleted=true`
- WHEN `DELETE .../members/{userId}` is called again
- THEN `404 Not Found` with `MEMBERS_NOT_FOUND` (soft-deleted rows are not resolved as removal
  targets)

---

### Requirement: MEMBERS-RESTORE-1 — Restore Member Access (WU2, security-critical)

The system MUST expose `POST /api/budgets/{budgetId}/members/{userId}/restore`, gated by the same
permission matrix as MEMBERS-REMOVE-1, applied using the role the member HELD BEFORE removal
(mirroring `RestoreBudget`'s precedent of resolving caller authorization independently of the
target's soft-deleted state). On success: `IsDeleted = false`, `DeletedAt = null`; the member's
`Role` is UNCHANGED by this endpoint (restore does not alter role — see MEMBERS-ROLE-1 for role
changes; the AcceptInvitation restore path in `auth` ACCEPT-1 is a distinct code path that DOES
set role from the new invitation). The system MUST evict
`budget-membership:{targetUserId}:{budgetId}` in the same request, before returning.

#### Scenario: Owner restores a previously-removed Operator (WU2)

- GIVEN a soft-deleted Operator membership, caller is Owner
- WHEN `POST .../restore` is called
- THEN `200 OK`; `IsDeleted=false`, `DeletedAt=null`, role unchanged from before removal; cache
  evicted

#### Scenario: Admin restores a previously-removed ReadOnly member (WU2)

- GIVEN a soft-deleted ReadOnly membership, caller is Admin
- WHEN `POST .../restore` is called
- THEN `200 OK` and the role is unchanged (still ReadOnly)

#### Scenario: Admin forbidden from restoring a previously-removed Admin (WU2)

- GIVEN a soft-deleted Admin membership, caller is Admin
- WHEN `POST .../restore` is called
- THEN `403 Forbidden` with `MEMBERS_CANNOT_ACT_ON_ADMIN`

#### Scenario: Restoring an already-active membership (WU2)

- GIVEN the target membership has `IsDeleted=false`
- WHEN `POST .../restore` is called
- THEN `409 Conflict` with `MEMBERS_NOT_DELETED`

**Note:** self-restore is structurally impossible — once removed, a member fails `budget:admin`
resolution for that budget entirely (per `auth` AUTHZ-1), so they cannot call this or any other
`budget:*`-gated endpoint for that budget on their own account. Re-invitation (ACCEPT-1) is the
only path back for a removed user.

---

### Requirement: MEMBERS-UI-1 — Members View (WU1 base, WU2 extended)

`BudgetMembersView.vue` MUST render MEMBERS-LIST-1's rows, excluding the Owner row entirely (no
role selector, no remove/restore control ever rendered for it). For every remaining row, role
`<select>` and remove/restore controls MUST be hidden for the caller's own row and MUST follow the
same permission matrix as MEMBERS-ROLE-1/MEMBERS-REMOVE-1 (e.g. an Admin sees no controls on
another Admin's row). The view MUST reuse `useRoleGate(budgetId).isAdmin`, not a locally
duplicated helper. A "show deleted" toggle (WU2) MUST be visible to any Owner or Admin — not
Owner-only — and when ON calls `GET /members?includeDeleted=true` and renders a Restore action per
row per the same matrix.

#### Scenario: Owner row excluded from the table (WU1)

- GIVEN the budget has an Owner and 2 other members
- WHEN `BudgetMembersView` renders
- THEN only the 2 non-Owner rows are displayed; no Owner row or role control exists for it

#### Scenario: No self-action controls (WU1)

- GIVEN the caller is an Admin viewing their own row
- WHEN that row renders
- THEN neither a role `<select>` nor a remove button is present for that row

#### Scenario: Admin sees no controls on another Admin's row (WU1)

- GIVEN the caller is Admin
- WHEN another Admin's row renders
- THEN no role `<select>` or remove button is present for that row

#### Scenario: Show-deleted toggle visible to Admin, not Owner-only (WU2)

- GIVEN the caller is Admin (not Owner)
- WHEN `BudgetMembersView` renders
- THEN the "show deleted" toggle is visible and functional

#### Scenario: Restore action shown per soft-deleted row (WU2)

- GIVEN the toggle is ON and a soft-deleted Operator row is present, caller is Owner or Admin
- WHEN the row renders
- THEN a Restore button is shown for that row
