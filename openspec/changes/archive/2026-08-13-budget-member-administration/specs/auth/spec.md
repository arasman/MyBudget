# Delta for Auth

## MODIFIED Requirements

### Requirement: ACCEPT-1 — Accept a Budget Invitation via Token

The system MUST validate the invitation token (hash comparison), verify it is not expired or
already used, and mark the invitation as used. Before creating a `BudgetMembership`, the system
MUST check whether a membership already exists for `(BudgetId, UserId)`:

- If an ACTIVE membership exists (`IsDeleted = false`) → the system MUST return a graceful
  `409 Conflict` with error code `AUTH_ALREADY_MEMBER` (the same code already emitted by
  `InviteUserToBudgetHandler` for the equivalent invite-time case) instead of attempting an insert.
- If a SOFT-DELETED membership exists (`IsDeleted = true`) → the system MUST RESTORE the existing
  row (`IsDeleted = false`, `DeletedAt = null`) and set its `Role` to the role from the invitation
  being accepted (NOT the pre-revocation role). It MUST NOT insert a new `BudgetMembership` row.
- If NO membership exists for `(BudgetId, UserId)` → the system creates a new `BudgetMembership`,
  as before, with the role from the invitation.

If the user does not yet have an account, they MUST register first — the accept endpoint requires
authentication.

(Previously: unconditionally called `BudgetMembership.Create(...)` + `Add(...)` with no existence
check, causing an unhandled `DbUpdateException` on the unique index `(BudgetId, UserId)` when a
second live invitation token was accepted for an already-member email.)

#### Scenario: Happy path — authenticated user accepts, no prior membership

- GIVEN an authenticated user and a valid, non-expired, unused invitation token for their email,
  and no existing `BudgetMembership` for `(BudgetId, UserId)`
- WHEN `POST /api/auth/invitations/accept` is called with `{ token }` and a valid Bearer token
- THEN the `Invitation` record has `UsedAt` set to now
- AND a new `BudgetMembership` is created for the user with the role from the invitation
- AND `200 OK` is returned with `{ budgetId, role }`, where `role` is serialized via the same
  `BudgetRoleStrings.ToApiString()` helper used elsewhere (WU0) — NOT raw `.ToString()` (which
  would emit PascalCase `"ReadOnly"` instead of the hyphenated `"read-only"` convention)

#### Scenario: Token expired

- GIVEN an invitation with `ExpiresAt` in the past
- WHEN `POST /api/auth/invitations/accept` is called
- THEN `410 Gone` is returned with error code `AUTH_INVITATION_EXPIRED`

#### Scenario: Token already used

- GIVEN an invitation with `UsedAt` already set
- WHEN `POST /api/auth/invitations/accept` is called
- THEN `410 Gone` is returned with error code `AUTH_INVITATION_ALREADY_USED`

#### Scenario: Token not found / invalid

- GIVEN a token value that does not match any `Invitation` record hash
- WHEN `POST /api/auth/invitations/accept` is called
- THEN `404 Not Found` is returned with error code `AUTH_INVITATION_NOT_FOUND`

#### Scenario: Email mismatch

- GIVEN an authenticated user with `email = "a@example.com"` and an invitation addressed to
  `"b@example.com"`
- WHEN `POST /api/auth/invitations/accept` is called
- THEN `403 Forbidden` is returned with error code `AUTH_INVITATION_EMAIL_MISMATCH`

#### Scenario: Unauthenticated request

- GIVEN no valid Bearer token in the `Authorization` header
- WHEN `POST /api/auth/invitations/accept` is called
- THEN `401 Unauthorized` is returned

#### Scenario: Second live invitation for an already-active member — graceful failure (WU0)

- GIVEN the user already has an ACTIVE `BudgetMembership` for budget X, AND a second valid,
  non-expired, unused invitation token exists for the same email/budget
- WHEN `POST /api/auth/invitations/accept` is called with the second token
- THEN `409 Conflict` is returned with error code `AUTH_ALREADY_MEMBER`
- AND no `DbUpdateException` is thrown
- AND the pre-existing membership row is untouched

#### Scenario: Re-invited, previously-removed member accepts — restore, not insert (WU0 extended by WU2)

- GIVEN the user has a SOFT-DELETED `BudgetMembership` for budget X with `Role = operator` (their
  role before removal), AND a new valid invitation for the same email/budget with `Role = read-only`
- WHEN `POST /api/auth/invitations/accept` is called with the new token
- THEN `200 OK` is returned
- AND the EXISTING membership row is restored: `IsDeleted = false`, `DeletedAt = null`
- AND its `Role` is updated to `read-only` (the invitation's role, NOT the pre-removal `operator`
  role)
- AND no new `BudgetMembership` row is inserted — exactly one row exists for `(BudgetId, UserId)`

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Token not found | 404 | `AUTH_INVITATION_NOT_FOUND` |
| Token expired | 410 | `AUTH_INVITATION_EXPIRED` |
| Token already used | 410 | `AUTH_INVITATION_ALREADY_USED` |
| Email mismatch | 403 | `AUTH_INVITATION_EMAIL_MISMATCH` |
| Unauthenticated | 401 | (standard Bearer challenge) |
| Active membership already exists (WU0) | 409 | `AUTH_ALREADY_MEMBER` |

**i18n keys (backend `.resx`):** `AcceptInvitation.Expired`, `AcceptInvitation.AlreadyUsed`,
`AcceptInvitation.NotFound`, `AcceptInvitation.EmailMismatch`,
`AcceptInvitation.AlreadyMember` (new)
**i18n keys (frontend `en.json` / `es.json`):** `invitation.accept.title`,
`invitation.accept.loading`, `invitation.accept.successMessage`, `invitation.accept.error.expired`,
`invitation.accept.error.alreadyUsed`, `invitation.accept.error.mismatch`,
`invitation.accept.error.alreadyMember` (new)

---

### Requirement: ME-1 — Authenticated Profile Retrieval

The system MUST return the current user's profile including a `memberships` array. Each membership
entry MUST include an `isDeleted` flag indicating whether the associated Budget is soft-deleted.
Soft-deleted memberships MUST be included in the response — they are NOT filtered out. Each
membership's `role` field MUST be serialized using the same kebab-case convention accepted by
`POST /api/budgets/{id}/invitations` (`owner`, `admin`, `operator`, `read-only`) — specifically,
the ReadOnly role MUST serialize as `"read-only"` (hyphenated), NOT `"readonly"`. This is a
read-only Dapper query — no EF Core. Requires valid JWT access token.

(Previously: `role` serialization convention was unspecified in this requirement;
`GetCurrentUserHandler.cs:52` emitted `"readonly"` — no hyphen — for the ReadOnly role, mismatching
the hyphenated form expected by `InviteUserToBudgetEndpoint.TryParseRole` and the frontend
`toRoleKey()` mapper, causing every ReadOnly member to render an untranslated i18n key.)

**Response shape:** `200 OK` with:
```
{
  id, email, firstName, lastName, preferredLocale, lastLoginAt, createdAt,
  memberships: [
    { budgetId, budgetName, role, isDeleted }
  ]
}
```

#### Scenario: Happy path — active memberships

- GIVEN an authenticated user with two active budget memberships
- WHEN `GET /api/auth/me` is called with `Authorization: Bearer {accessToken}`
- THEN `200 OK` is returned with user profile fields
- AND `memberships` array contains two entries each with `isDeleted: false`

#### Scenario: Soft-deleted membership included

- GIVEN an authenticated user with one active and one soft-deleted budget membership
- WHEN `GET /api/auth/me` is called
- THEN `200 OK` is returned
- AND `memberships` contains two entries: one with `isDeleted: false`, one with `isDeleted: true`

#### Scenario: All memberships deleted

- GIVEN an authenticated user whose only budget has `IsDeleted = true`
- WHEN `GET /api/auth/me` is called
- THEN `200 OK` is returned
- AND `memberships` contains one entry with `isDeleted: true`

#### Scenario: Expired access token

- GIVEN an expired JWT
- WHEN `GET /api/auth/me` is called
- THEN `401 Unauthorized` is returned (standard Bearer challenge)

#### Scenario: ReadOnly role serializes with a hyphen (WU0)

- GIVEN an authenticated user with a `read-only` role membership
- WHEN `GET /api/auth/me` is called
- THEN the response's membership entry has `role: "read-only"` — not `"readonly"`

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Missing / expired token | 401 | (standard Bearer challenge) |

---

### Requirement: AUTHZ-1 — Role Resolution at Request Time

The system MUST resolve the current user's role for a given budget by querying `BudgetMembership`
at request time. This MUST NOT rely on any role stored in the JWT. Role resolution MUST exclude
soft-deleted `BudgetMembership` rows (`IsDeleted = true`). A user whose only membership row for a
budget has `IsDeleted = true` MUST be treated identically to a user with no membership record:
`403 AUTH_NOT_A_MEMBER`. This exclusion applies both to the Dapper fallback query AND to what is
written to/read from the cache — an `IsDeleted = true` row MUST NOT be cached as an authorizing
role, nor served from a stale cache entry after removal.

**Role hierarchy (descending privilege):** `owner` > `admin` > `operator` > `read-only`

**Caching:** The authorization handler SHOULD use a short-TTL in-memory cache (keyed by
`userId + budgetId`, TTL ≤ 5 minutes) to avoid N+1 DB queries per request. Cache MUST be
invalidated synchronously when membership changes (role change, remove, restore).

(Previously: the Dapper role-resolution query and cache did not filter membership by `IsDeleted`,
because `BudgetMembership` had no soft-delete column. This is the security-critical WU2 change —
every `budget:*`-gated endpoint in the app resolves through this same query/cache.)

#### Scenario: Authorized request

- GIVEN user has `admin` role in budget `{id}` (from `BudgetMembership`, `IsDeleted = false`)
- WHEN a protected endpoint requiring minimum `admin` role is called
- THEN the request is allowed through

#### Scenario: Insufficient role

- GIVEN user has `operator` role in budget `{id}`
- WHEN a protected endpoint requiring `admin` or higher is called
- THEN `403 Forbidden` is returned with error code `AUTH_INSUFFICIENT_ROLE`

#### Scenario: No membership

- GIVEN user has no `BudgetMembership` record for budget `{id}`
- WHEN any protected budget endpoint is called
- THEN `403 Forbidden` is returned with error code `AUTH_NOT_A_MEMBER`

#### Scenario: JWT has no roles

- GIVEN a valid JWT with no role claims
- WHEN a protected budget endpoint is called
- THEN role resolution is performed exclusively via DB/cache lookup — the JWT role field is never
  consulted

#### Scenario: Soft-deleted membership resolves as no-membership (WU2, security-critical)

- GIVEN a user has a `BudgetMembership` for budget X with `IsDeleted = true`
- WHEN a protected `budget:*` endpoint for budget X is called
- THEN `403 Forbidden` is returned with error code `AUTH_NOT_A_MEMBER` — identical to having no
  membership row at all

#### Scenario: Restored membership resolves normally again (WU2)

- GIVEN a previously soft-deleted membership is restored (`IsDeleted = false`)
- WHEN the same protected endpoint is called again
- THEN the request is authorized per the restored role

#### Scenario: Existing budget:*-gated endpoints unaffected for active memberships (WU2, regression guard)

- GIVEN a user with an ACTIVE (`IsDeleted = false`) membership at any role
- WHEN any existing `budget:*`-gated endpoint is called
- THEN authorization behaves exactly as before this change — no new `403` for active memberships

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Role below required threshold | 403 | `AUTH_INSUFFICIENT_ROLE` |
| No membership for budget (incl. soft-deleted) | 403 | `AUTH_NOT_A_MEMBER` |
