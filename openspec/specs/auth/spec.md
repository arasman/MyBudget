# Auth Feature Specification

## Purpose

This spec covers all authentication and authorization behavior introduced by the `auth` change.
It defines what the system MUST do — not how to implement it.
All 7 capabilities are new; no existing auth spec exists.

---

## Shared Constraints

- SC-1: The application MUST fail to start if `JWT__Key` is not configured in the environment (User Secrets in dev, env var in prod). It MUST NOT fall back to a default value.
- SC-2: `JWT__Key` MUST NOT appear in `appsettings.json` or any committed configuration file.
- SC-3: The EF Core migration `AddAuthTables` MUST be the only migration that touches auth tables. `InitialCreate` MUST NOT be modified.
- SC-4: All JWT tokens MUST contain only: `sub` (userId), `email`, `jti`, `iat`, `exp`. No roles, no budget IDs.
- SC-5: Budget roles are NEVER baked into JWT — they are resolved at request time from `BudgetMembership`.

---

## Capability 1: User Registration

### Requirement: REG-1 — Account Creation

The system MUST create a user account, hash the password with BCrypt (workFactor 12), persist a default budget, and return a JWT pair on success.

**Field validation rules:**
| Field | Rule |
|---|---|
| `email` | REQUIRED. Valid email format (RFC 5322). Max 254 chars. Case-insensitive unique. |
| `password` | REQUIRED. Min 8 chars, max 72 chars. Must contain at least 1 uppercase, 1 lowercase, 1 digit. |
| `firstName` | REQUIRED. Min 1 char, max 100 chars. Trimmed. |
| `lastName` | REQUIRED. Min 1 char, max 100 chars. Trimmed. |
| `preferredLocale` | OPTIONAL. If provided, must be `"en"` or `"es"`. Defaults to `"en"`. |

**Post-registration side effects (all atomic in same transaction):**
- A `Budget` record MUST be created with `Name = "{firstName}'s Budget"`, `OwnerId = newUser.Id`.
- A `BudgetMembership` record MUST be created with `Role = owner`, linking the new user to the new budget.

**Response on success:** `201 Created` with `{ accessToken, refreshToken, expiresIn, user: { id, email, firstName, lastName, preferredLocale } }`.

#### Scenario: Happy path — valid registration

- GIVEN no account exists with the provided email
- WHEN `POST /api/auth/register` is called with valid `email`, `password`, `firstName`, `lastName`
- THEN a `User`, `Budget`, and `BudgetMembership (owner)` are created in one transaction
- AND a `201` response is returned with `accessToken`, `refreshToken`, and user profile
- AND the refresh token is stored as a BCrypt hash in `RefreshToken` table

#### Scenario: Duplicate email

- GIVEN an account already exists with `email = "user@example.com"`
- WHEN `POST /api/auth/register` is called with the same email (any casing)
- THEN the system returns `409 Conflict` with error code `AUTH_EMAIL_TAKEN`

#### Scenario: Password too weak

- GIVEN a registration request with `password = "abc123"` (no uppercase)
- WHEN `POST /api/auth/register` is called
- THEN the system returns `422 Unprocessable Entity` with field error `password: AUTH_PASSWORD_TOO_WEAK`

#### Scenario: Missing required field

- GIVEN a registration request with `firstName` omitted
- WHEN `POST /api/auth/register` is called
- THEN the system returns `422 Unprocessable Entity` with field error `firstName: FIELD_REQUIRED`

#### Scenario: Invalid preferredLocale

- GIVEN a registration request with `preferredLocale = "fr"`
- WHEN `POST /api/auth/register` is called
- THEN the system returns `422 Unprocessable Entity` with field error `preferredLocale: AUTH_LOCALE_UNSUPPORTED`

#### Scenario: Email placeholder renders without vue-i18n warning

- GIVEN the register view renders with locale "es"
- WHEN the email input placeholder is displayed
- THEN no vue-i18n linked-message warning appears in the browser console

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Email already taken | 409 | `AUTH_EMAIL_TAKEN` |
| Password too weak | 422 | `AUTH_PASSWORD_TOO_WEAK` |
| Field missing/invalid | 422 | `FIELD_REQUIRED` / `FIELD_INVALID` |
| Locale not supported | 422 | `AUTH_LOCALE_UNSUPPORTED` |

**i18n keys (backend `.resx`):** `RegisterUser.EmailTaken`, `RegisterUser.PasswordTooWeak`, `RegisterUser.LocaleUnsupported`
**i18n keys (frontend `en.json` / `es.json`):** `auth.register.title`, `auth.register.emailPlaceholder`, `auth.register.passwordPlaceholder`, `auth.register.firstNamePlaceholder`, `auth.register.lastNamePlaceholder`, `auth.register.submit`, `auth.register.loginLink`, `auth.register.successMessage`, `auth.register.languageLabel`

---

### Requirement: REG-I18N-1 — Language Label i18n Key

The register view MUST use the i18n key `auth.register.languageLabel` for the language selector label. No hardcoded label text MAY appear at `RegisterView.vue:152` or any equivalent location.

#### Scenario: Language label rendered from i18n

- GIVEN locale is "es"
- WHEN the register view renders
- THEN the language selector label shows the Spanish translation for `auth.register.languageLabel`
- AND no hardcoded "Language" string is present in the DOM

---

## Capability 2: User Login

### Requirement: LOGIN-1 — Credential Verification and Token Issuance

The system MUST verify credentials, issue a JWT access token (15-minute TTL) and a rotating refresh token (7-day TTL, single-use, hashed in DB), and update `LastLoginAt`. The `auth.login.emailPlaceholder` i18n key MUST escape `@` as `{'@'}` to prevent vue-i18n linked-message errors.

**Field validation rules:**
| Field | Rule |
|---|---|
| `email` | REQUIRED. Valid email format. |
| `password` | REQUIRED. Non-empty. |

**Response on success:** `200 OK` with `{ accessToken, refreshToken, expiresIn: 900, user: { id, email, firstName, lastName, preferredLocale } }`.

#### Scenario: Happy path — valid credentials

- GIVEN a user exists with `email` and a matching BCrypt hash
- WHEN `POST /api/auth/login` is called with correct credentials
- THEN `200 OK` is returned with a fresh `accessToken` (TTL 15 min) and `refreshToken` (TTL 7 days)
- AND `LastLoginAt` is updated
- AND the `refreshToken` value is stored hashed in `RefreshToken` table

#### Scenario: Wrong password

- GIVEN a user exists with `email = "user@example.com"`
- WHEN `POST /api/auth/login` is called with an incorrect password
- THEN `401 Unauthorized` is returned with error code `AUTH_INVALID_CREDENTIALS`
- AND the response time MUST NOT reveal whether the email exists (constant-time comparison)

#### Scenario: Unknown email

- GIVEN no account exists with `email = "ghost@example.com"`
- WHEN `POST /api/auth/login` is called
- THEN `401 Unauthorized` is returned with error code `AUTH_INVALID_CREDENTIALS` (same as wrong password — no enumeration)

#### Scenario: Missing field

- GIVEN a login request with `password` omitted
- WHEN `POST /api/auth/login` is called
- THEN `422 Unprocessable Entity` is returned with field error `password: FIELD_REQUIRED`

#### Scenario: Email placeholder renders without vue-i18n warning

- GIVEN the login view renders with locale "en"
- WHEN the email input placeholder is displayed
- THEN no vue-i18n linked-message warning appears in the browser console

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Wrong email or password | 401 | `AUTH_INVALID_CREDENTIALS` |
| Field missing/invalid | 422 | `FIELD_REQUIRED` / `FIELD_INVALID` |

**i18n keys (backend `.resx`):** `LoginUser.InvalidCredentials`
**i18n keys (frontend `en.json` / `es.json`):** `auth.login.title`, `auth.login.emailPlaceholder`, `auth.login.passwordPlaceholder`, `auth.login.submit`, `auth.login.registerLink`, `auth.login.error.invalidCredentials`

---

## Capability 3: Token Refresh

### Requirement: REFRESH-1 — Silent Re-authentication via Rotating Refresh Token

The system MUST validate the submitted refresh token (hash comparison), issue a new access token and a new refresh token, and mark the old token as revoked (replaced). If a refresh token is submitted that has already been used (reuse detected), the system MUST revoke ALL tokens in that token family (theft detection).

**Token family:** all `RefreshToken` records linked by `ReplacedByTokenId` chain originating from the same login.

**TTLs:** new access token = 15 min, new refresh token = 7 days.

#### Scenario: Happy path — valid refresh

- GIVEN a valid, non-expired, non-revoked refresh token exists in DB
- WHEN `POST /api/auth/refresh` is called with that token
- THEN `200 OK` is returned with a new `accessToken` and `refreshToken`
- AND the old refresh token record has `RevokedAt` set and `ReplacedByTokenId` pointing to the new token

#### Scenario: Reuse detection (theft)

- GIVEN a refresh token that was already used (its `RevokedAt` is set and it has a `ReplacedByTokenId`)
- WHEN `POST /api/auth/refresh` is called with that stale token
- THEN all tokens in the same family are revoked (`RevokedAt` set on all)
- AND `401 Unauthorized` is returned with error code `AUTH_REFRESH_TOKEN_REUSE`

#### Scenario: Expired refresh token

- GIVEN a refresh token with `ExpiresAt` in the past
- WHEN `POST /api/auth/refresh` is called
- THEN `401 Unauthorized` is returned with error code `AUTH_REFRESH_TOKEN_EXPIRED`

#### Scenario: Unknown token

- GIVEN a refresh token value that does not match any DB record
- WHEN `POST /api/auth/refresh` is called
- THEN `401 Unauthorized` is returned with error code `AUTH_REFRESH_TOKEN_INVALID`

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Reuse / theft detected | 401 | `AUTH_REFRESH_TOKEN_REUSE` |
| Token expired | 401 | `AUTH_REFRESH_TOKEN_EXPIRED` |
| Token not found | 401 | `AUTH_REFRESH_TOKEN_INVALID` |

**i18n keys (backend `.resx`):** `RefreshToken.Reuse`, `RefreshToken.Expired`, `RefreshToken.Invalid`

---

## Capability 4: User Logout

### Requirement: LOGOUT-1 — Server-Side Token Revocation

The system MUST revoke the submitted refresh token by setting its `RevokedAt` timestamp. The endpoint requires a valid JWT access token in the `Authorization: Bearer` header.

#### Scenario: Happy path — authenticated logout

- GIVEN an authenticated user with a valid access token and a valid refresh token
- WHEN `POST /api/auth/logout` is called with `{ refreshToken }` in the body and `Authorization: Bearer {accessToken}` header
- THEN the matching `RefreshToken` record has `RevokedAt` set to now
- AND `200 OK` is returned with `{ message: "Logged out" }`

#### Scenario: Unauthenticated request

- GIVEN no `Authorization` header or an expired access token
- WHEN `POST /api/auth/logout` is called
- THEN `401 Unauthorized` is returned

#### Scenario: Token not found or already revoked

- GIVEN a refresh token value that does not match any active record for the current user
- WHEN `POST /api/auth/logout` is called
- THEN `200 OK` is returned (idempotent — no error exposed)

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Missing / invalid access token | 401 | (standard Bearer challenge) |

---

## Capability 5: Current User

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

## Capability 6: Budget Invitation

### Requirement: INV-1 — Invite a User to a Budget by Email

The system MUST allow a budget member with `owner` or `admin` role to invite an email address with a specific role. An invitation token (random, 256-bit) is generated, hashed for storage, and sent via the MailKit + Channel pipeline. Invitation TTL: 72 hours.

**Field validation rules:**
| Field | Rule |
|---|---|
| `email` | REQUIRED. Valid email format. Max 254 chars. |
| `role` | REQUIRED. One of: `admin`, `operator`, `read-only`. (Cannot invite as `owner`.) |

**Invited role constraint:** `owner` MUST NOT be a valid invitation role.

#### Scenario: Happy path — valid invitation

- GIVEN an authenticated user with `owner` or `admin` role in budget `{id}`
- WHEN `POST /api/budgets/{id}/invitations` is called with valid `email` and `role`
- THEN an `Invitation` record is created with `ExpiresAt = now + 72h`, `TokenHash` = hashed token
- AND an email is queued via Channel to the invitee's address with the plain token in the invite link
- AND `201 Created` is returned with `{ invitationId, expiresAt }`

#### Scenario: Unauthorized — wrong role

- GIVEN an authenticated user with `operator` role in budget `{id}`
- WHEN `POST /api/budgets/{id}/invitations` is called
- THEN `403 Forbidden` is returned with error code `AUTH_INSUFFICIENT_ROLE`

#### Scenario: Inviting an owner role

- GIVEN a valid `owner` or `admin` caller
- WHEN `POST /api/budgets/{id}/invitations` is called with `role = "owner"`
- THEN `422 Unprocessable Entity` is returned with error code `AUTH_CANNOT_INVITE_AS_OWNER`

#### Scenario: Invitee already a member

- GIVEN the target email already has a `BudgetMembership` for budget `{id}`
- WHEN `POST /api/budgets/{id}/invitations` is called
- THEN `409 Conflict` is returned with error code `AUTH_ALREADY_MEMBER`

#### Scenario: Budget not found

- GIVEN budget `{id}` does not exist
- WHEN `POST /api/budgets/{id}/invitations` is called
- THEN `404 Not Found` is returned with error code `BUDGET_NOT_FOUND`

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Caller lacks owner/admin role | 403 | `AUTH_INSUFFICIENT_ROLE` |
| Inviting as owner | 422 | `AUTH_CANNOT_INVITE_AS_OWNER` |
| Invitee already a member | 409 | `AUTH_ALREADY_MEMBER` |
| Budget not found | 404 | `BUDGET_NOT_FOUND` |
| Invalid field | 422 | `FIELD_REQUIRED` / `FIELD_INVALID` |

**i18n keys (backend `.resx`):** `InviteUserToBudget.InsufficientRole`, `InviteUserToBudget.CannotInviteAsOwner`, `InviteUserToBudget.AlreadyMember`, `InviteUserToBudget.EmailSubject`, `InviteUserToBudget.EmailBody`
**i18n keys (frontend `en.json` / `es.json`):** `invitation.modal.title`, `invitation.modal.emailLabel`, `invitation.modal.roleLabel`, `invitation.modal.submit`, `invitation.modal.successMessage`, `invitation.modal.error.alreadyMember`

---

## Capability 7: Accept Invitation

### Requirement: ACCEPT-1 — Accept a Budget Invitation via Token

The system MUST validate the invitation token (hash comparison), verify it is not expired or already used, and mark the invitation as used. Before creating a `BudgetMembership`, the system MUST check whether a membership already exists for `(BudgetId, UserId)`:

- If an ACTIVE membership exists (`IsDeleted = false`) → the system MUST return a graceful `409 Conflict` with error code `AUTH_ALREADY_MEMBER` (the same code already emitted by `InviteUserToBudgetHandler` for the equivalent invite-time case) instead of attempting an insert.
- If a SOFT-DELETED membership exists (`IsDeleted = true`) → the system MUST RESTORE the existing row (`IsDeleted = false`, `DeletedAt = null`) and set its `Role` to the role from the invitation being accepted (NOT the pre-revocation role). It MUST NOT insert a new `BudgetMembership` row.
- If NO membership exists for `(BudgetId, UserId)` → the system creates a new `BudgetMembership`, as before, with the role from the invitation.

If the user does not yet have an account, they MUST register first — the accept endpoint requires authentication.

(Previously: unconditionally called `BudgetMembership.Create(...)` + `Add(...)` with no existence check, causing an unhandled `DbUpdateException` on the unique index `(BudgetId, UserId)` when a second live invitation token was accepted for an already-member email.)

#### Scenario: Happy path — authenticated user accepts, no prior membership

- GIVEN an authenticated user and a valid, non-expired, unused invitation token for their email, and no existing `BudgetMembership` for `(BudgetId, UserId)`
- WHEN `POST /api/auth/invitations/accept` is called with `{ token }` and a valid Bearer token
- THEN the `Invitation` record has `UsedAt` set to now
- AND a new `BudgetMembership` is created for the user with the role from the invitation
- AND `200 OK` is returned with `{ budgetId, role }`, where `role` is serialized via the same `BudgetRoleStrings.ToApiString()` helper used elsewhere (WU0) — NOT raw `.ToString()` (which would emit PascalCase `"ReadOnly"` instead of the hyphenated `"read-only"` convention)

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

- GIVEN an authenticated user with `email = "a@example.com"` and an invitation addressed to `"b@example.com"`
- WHEN `POST /api/auth/invitations/accept` is called
- THEN `403 Forbidden` is returned with error code `AUTH_INVITATION_EMAIL_MISMATCH`

#### Scenario: Unauthenticated request

- GIVEN no valid Bearer token in the `Authorization` header
- WHEN `POST /api/auth/invitations/accept` is called
- THEN `401 Unauthorized` is returned

#### Scenario: Second live invitation for an already-active member — graceful failure (WU0)

- GIVEN the user already has an ACTIVE `BudgetMembership` for budget X, AND a second valid, non-expired, unused invitation token exists for the same email/budget
- WHEN `POST /api/auth/invitations/accept` is called with the second token
- THEN `409 Conflict` is returned with error code `AUTH_ALREADY_MEMBER`
- AND no `DbUpdateException` is thrown
- AND the pre-existing membership row is untouched

#### Scenario: Re-invited, previously-removed member accepts — restore, not insert (WU0 extended by WU2)

- GIVEN the user has a SOFT-DELETED `BudgetMembership` for budget X with `Role = operator` (their role before removal), AND a new valid invitation for the same email/budget with `Role = read-only`
- WHEN `POST /api/auth/invitations/accept` is called with the new token
- THEN `200 OK` is returned
- AND the EXISTING membership row is restored: `IsDeleted = false`, `DeletedAt = null`
- AND its `Role` is updated to `read-only` (the invitation's role, NOT the pre-removal `operator` role)
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

**i18n keys (backend `.resx`):** `AcceptInvitation.Expired`, `AcceptInvitation.AlreadyUsed`, `AcceptInvitation.NotFound`, `AcceptInvitation.EmailMismatch`, `AcceptInvitation.AlreadyMember` (new)
**i18n keys (frontend `en.json` / `es.json`):** `invitation.accept.title`, `invitation.accept.loading`, `invitation.accept.successMessage`, `invitation.accept.error.expired`, `invitation.accept.error.alreadyUsed`, `invitation.accept.error.mismatch`, `invitation.accept.error.alreadyMember` (new)

---

## Capability 8: Per-Budget Authorization

### Requirement: AUTHZ-1 — Role Resolution at Request Time

The system MUST resolve the current user's role for a given budget by querying `BudgetMembership` at request time. This MUST NOT rely on any role stored in the JWT. Role resolution MUST exclude soft-deleted `BudgetMembership` rows (`IsDeleted = true`). A user whose only membership row for a budget has `IsDeleted = true` MUST be treated identically to a user with no membership record: `403 AUTH_NOT_A_MEMBER`. This exclusion applies both to the Dapper fallback query AND to what is written to/read from the cache — an `IsDeleted = true` row MUST NOT be cached as an authorizing role, nor served from a stale cache entry after removal.

**Role hierarchy (descending privilege):** `owner` > `admin` > `operator` > `read-only`

**Caching:** The authorization handler SHOULD use a short-TTL in-memory cache (keyed by `userId + budgetId`, TTL ≤ 5 minutes) to avoid N+1 DB queries per request. Cache MUST be invalidated synchronously when membership changes (role change, remove, restore).

(Previously: the Dapper role-resolution query and cache did not filter membership by `IsDeleted`, because `BudgetMembership` had no soft-delete column. This is the security-critical WU2 change — every `budget:*`-gated endpoint in the app resolves through this same query/cache.)

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
- THEN role resolution is performed exclusively via DB/cache lookup — the JWT role field is never consulted

#### Scenario: Soft-deleted membership resolves as no-membership (WU2, security-critical)

- GIVEN a user has a `BudgetMembership` for budget X with `IsDeleted = true`
- WHEN a protected `budget:*` endpoint for budget X is called
- THEN `403 Forbidden` is returned with error code `AUTH_NOT_A_MEMBER` — identical to having no membership row at all

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

---

## Capability 13: Locale Management

### Requirement: AUTH-LOCALE-1 — PATCH Locale Endpoint

The system MUST expose `PATCH /api/auth/me/locale` for any authenticated user to update their own `PreferredLocale`. This requirement is fully specified in `openspec/specs/locale-sync/spec.md` as LSYNC-3.

#### Scenario: Authenticated user updates own locale

- GIVEN a valid JWT is present in the request
- WHEN `PATCH /api/auth/me/locale` is called with `{ locale: "es" }`
- THEN `User.PreferredLocale` is updated for the token owner
- AND the response is `204 No Content`

---

### Requirement: AUTH-LOCALE-2 — Locale Seeding from fetchMe

After a successful login, the system MUST read `preferredLocale` from the `GET /api/auth/me` response and conditionally apply it to the frontend locale store. This requirement is fully specified in `openspec/specs/locale-sync/spec.md` as LSYNC-1.

#### Scenario: fetchMe response includes preferredLocale

- GIVEN the user is authenticated
- WHEN `GET /api/auth/me` is called
- THEN the response body includes a `preferredLocale` field with a value of `"en"` or `"es"`

---

## Startup Guard

### Requirement: STARTUP-1 — JWT Key Configuration Guard

The system MUST validate that `JWT__Key` is present and non-empty during application startup (before any request is served). If absent, the application MUST throw an exception that prevents startup.

#### Scenario: Key missing at startup

- GIVEN the environment has no `JWT__Key` value configured
- WHEN the application starts
- THEN startup fails with a descriptive exception: `"JWT__Key is not configured. Set it via User Secrets (dev) or environment variable (prod)."`
- AND no HTTP endpoints become available

#### Scenario: Key present at startup

- GIVEN `JWT__Key` is set via User Secrets or env var
- WHEN the application starts
- THEN startup succeeds normally

---

## Capability 9: Password Recovery

### Requirement: REQ-PWD-1 — Forgot-Password Request

The system MUST accept an anonymous `POST /api/auth/forgot-password` request and always return `200 OK`, regardless of whether the email address is registered (anti-enumeration). When the email matches a registered user, a password-reset token is generated, hashed, stored in `PasswordResetTokens`, and an email with a reset link is queued via `EmailChannel`.

**Request body:** `{ email: string }`

**Token lifecycle:**
- `ExpiresAt = now + PasswordPolicy:ResetTokenExpiryMinutes` (default: 30)
- Multiple pending tokens MAY coexist per user; only the latest valid token accepted at reset time is effective
- Token is single-use: `UsedAt` is set on consumption

**Email link format:** `{frontend-base-url}/reset-password?token={plainToken}`

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| `email` field missing or invalid format | 422 | `FIELD_REQUIRED` / `FIELD_INVALID` |
| User not found | 200 | (no-op — same response as success) |

#### Scenario: Happy path — registered email

- GIVEN a user exists with `email = "user@example.com"`
- WHEN `POST /api/auth/forgot-password` is called with `{ email: "user@example.com" }`
- THEN `200 OK` is returned
- AND a `PasswordResetToken` record is created with `ExpiresAt = now + 30min`, `UsedAt = null`
- AND an email is queued with the plain token embedded in the reset link

#### Scenario: Unknown email — no enumeration

- GIVEN no account exists with `email = "ghost@example.com"`
- WHEN `POST /api/auth/forgot-password` is called with `{ email: "ghost@example.com" }`
- THEN `200 OK` is returned
- AND no `PasswordResetToken` is created
- AND no email is queued

#### Scenario: Missing email field

- GIVEN a request body with `email` omitted
- WHEN `POST /api/auth/forgot-password` is called
- THEN `422 Unprocessable Entity` is returned with field error `email: FIELD_REQUIRED`

---

### Requirement: REQ-PWD-2 — Reset Password via Token

The system MUST accept an anonymous `POST /api/auth/reset-password` request, validate the token, set the new password, clear all security flags, invalidate ALL existing refresh tokens for the user, and write a `PasswordChanged` audit event.

**Request body:** `{ token: string, newPassword: string }`

**Token validation sequence:**
1. Lookup `PasswordResetToken` records for the token value via BCrypt.Verify against stored hashes
2. Reject if no matching record found → `PWD_TOKEN_INVALID` (404)
3. Reject if `ExpiresAt <= now` → `PWD_TOKEN_EXPIRED` (410)
4. Reject if `UsedAt != null` (already used) → `PWD_TOKEN_INVALID` (404)

**State changes on success (all in single SaveChangesAsync):**
- `User.PasswordHash` = BCrypt hash of `newPassword` (workFactor 12)
- `User.PasswordChangedAt` = now (UTC)
- `User.FailedLoginAttempts` = 0
- `User.LockoutUntil` = null
- `User.ForcePasswordChange` = false
- `PasswordResetToken.UsedAt` = now
- ALL `RefreshToken` records for the user with `RevokedAt = null` → `RevokedAt = now`

**Audit event:** `PasswordChanged` written via `ISecurityAuditWriter` with `UserId`, `IpAddress`, `UserAgent`.

**Response on success:** `200 OK`

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Token not found or already used | 404 | `PWD_TOKEN_INVALID` |
| Token expired | 410 | `PWD_TOKEN_EXPIRED` |
| `newPassword` too weak | 422 | `PWD_PASSWORD_TOO_WEAK` |
| Field missing | 422 | `FIELD_REQUIRED` |

#### Scenario: Happy path — valid token

- GIVEN a valid, non-expired, unused `PasswordResetToken` exists for `userId = "abc"`
- WHEN `POST /api/auth/reset-password` is called with the matching plain token and a valid new password
- THEN `200 OK` is returned
- AND `User.PasswordHash` is updated, `PasswordChangedAt` is set to now
- AND `FailedLoginAttempts = 0`, `LockoutUntil = null`, `ForcePasswordChange = false`
- AND `PasswordResetToken.UsedAt` is set
- AND all previously active refresh tokens for `userId = "abc"` are revoked
- AND a `PasswordChanged` SecurityAuditLog event is written

#### Scenario: Expired token

- GIVEN a `PasswordResetToken` with `ExpiresAt` 1 minute in the past
- WHEN `POST /api/auth/reset-password` is called with the matching token
- THEN `410 Gone` is returned with error code `PWD_TOKEN_EXPIRED`
- AND no state changes occur

#### Scenario: Invalid or already-used token

- GIVEN a token value that matches no valid `PasswordResetToken` record (not found or `UsedAt` set)
- WHEN `POST /api/auth/reset-password` is called
- THEN `404 Not Found` is returned with error code `PWD_TOKEN_INVALID`
- AND no state changes occur

#### Scenario: Weak new password

- GIVEN a valid token
- WHEN `POST /api/auth/reset-password` is called with `newPassword = "abc123"` (no uppercase)
- THEN `422 Unprocessable Entity` is returned with field error `newPassword: PWD_PASSWORD_TOO_WEAK`

---

## Capability 10: Authenticated Password Change

### Requirement: REQ-PWD-3 — Change Password (Authenticated)

The system MUST accept an authenticated `POST /api/auth/change-password` request, verify the current password against the stored hash, set the new password, clear the forced-change flag, invalidate ALL refresh tokens except the one associated with the current session, and write a `PasswordChanged` audit event.

**Request body:** `{ currentPassword: string, newPassword: string }`

**Requires:** valid `Authorization: Bearer {accessToken}` header.

**State changes on success (all in single SaveChangesAsync):**
- `User.PasswordHash` = BCrypt hash of `newPassword` (workFactor 12)
- `User.PasswordChangedAt` = now (UTC)
- `User.ForcePasswordChange` = false
- ALL `RefreshToken` records for the user with `RevokedAt = null`, excluding the current session's refresh token → `RevokedAt = now`

**Note:** The current session's refresh token is preserved so the user remains logged in after the password change.

**Audit event:** `PasswordChanged` written via `ISecurityAuditWriter` with `UserId`, `IpAddress`, `UserAgent`.

**Response on success:** `200 OK`

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| `currentPassword` does not match stored hash | 400 | `PWD_CURRENT_INCORRECT` |
| `newPassword` too weak | 422 | `PWD_PASSWORD_TOO_WEAK` |
| Field missing | 422 | `FIELD_REQUIRED` |
| Unauthenticated | 401 | (standard Bearer challenge) |

#### Scenario: Happy path — correct current password

- GIVEN an authenticated user with a valid access token
- WHEN `POST /api/auth/change-password` is called with the correct `currentPassword` and a valid `newPassword`
- THEN `200 OK` is returned
- AND `User.PasswordHash` is updated, `PasswordChangedAt` is set to now, `ForcePasswordChange = false`
- AND all other refresh tokens are revoked, current session token preserved
- AND a `PasswordChanged` SecurityAuditLog event is written

#### Scenario: Wrong current password

- GIVEN an authenticated user
- WHEN `POST /api/auth/change-password` is called with an incorrect `currentPassword`
- THEN `400 Bad Request` is returned with error code `PWD_CURRENT_INCORRECT`
- AND no state changes occur

#### Scenario: Unauthenticated

- GIVEN no `Authorization` header
- WHEN `POST /api/auth/change-password` is called
- THEN `401 Unauthorized` is returned

---

## Capability 11: Login Lockout (LoginUser modification)

### Requirement: REQ-PWD-4 — Account Lockout After Repeated Failures

The existing `LoginUser` handler MUST be modified to check account lockout status BEFORE performing BCrypt.Verify, and to increment the failure counter and trigger lockout on each BCrypt failure.

**Lockout check (BEFORE BCrypt.Verify):**
- If `User.LockoutUntil != null AND User.LockoutUntil > now (UTC)` → return `AUTH_ACCOUNT_LOCKED` (423) immediately without performing BCrypt.Verify

**BCrypt.Verify failure path:**
- Increment `User.FailedLoginAttempts`
- If `FailedLoginAttempts >= PasswordPolicy:MaxFailedAttempts` (default: 5):
  - Set `User.LockoutUntil = now + PasswordPolicy:LockoutDurationMinutes` (default: 30)
  - Write `AccountLocked` SecurityAuditLog event with `UserId`, `IpAddress`, `UserAgent`, `FailedAttempts` count
- Save changes
- Return `AUTH_INVALID_CREDENTIALS` (401) — same code as before, no distinction

**BCrypt.Verify success path (reset):**
- Set `User.FailedLoginAttempts = 0`
- Set `User.LockoutUntil = null`
- Continue with existing token issuance logic

**Error responses (additions to LOGIN-1):**
| Condition | HTTP | Error Code |
|---|---|---|
| Account currently locked | 423 | `AUTH_ACCOUNT_LOCKED` |

#### Scenario: Lockout triggered after N failures

- GIVEN a user exists with `FailedLoginAttempts = 4` and `MaxFailedAttempts = 5`
- WHEN `POST /api/auth/login` is called with an incorrect password
- THEN `FailedLoginAttempts` becomes 5, `LockoutUntil = now + 30min`
- AND an `AccountLocked` SecurityAuditLog event is written
- AND `401 Unauthorized` is returned with error code `AUTH_INVALID_CREDENTIALS`

#### Scenario: Locked account attempt

- GIVEN a user with `LockoutUntil = (now + 10min)`
- WHEN `POST /api/auth/login` is called (any password)
- THEN `423 Locked` is returned with error code `AUTH_ACCOUNT_LOCKED`
- AND BCrypt.Verify is NOT performed
- AND `FailedLoginAttempts` is NOT incremented

#### Scenario: Lockout clears after successful reset-password

- GIVEN a user locked with `LockoutUntil = (now + 20min)` and `FailedLoginAttempts = 5`
- WHEN `POST /api/auth/reset-password` is called with a valid token
- THEN `LockoutUntil = null`, `FailedLoginAttempts = 0`
- AND subsequent login with correct credentials succeeds

#### Scenario: Successful login resets failure counter

- GIVEN a user with `FailedLoginAttempts = 3`, `LockoutUntil = null`
- WHEN `POST /api/auth/login` is called with correct credentials
- THEN `200 OK` is returned
- AND `User.FailedLoginAttempts = 0`, `User.LockoutUntil = null`

---

## Capability 12: Forced Password Change (LoginUser modification)

### Requirement: REQ-PWD-5 — Forced-Change Detection at Login

The existing `LoginUser` handler MUST be modified to detect forced-change conditions AFTER successful BCrypt.Verify. When either condition is true, the system MUST NOT issue tokens and MUST return `AUTH_FORCE_PASSWORD_CHANGE`.

**Forced-change conditions (checked after successful BCrypt.Verify):**
1. `User.ForcePasswordChange == true`
2. `PasswordPolicy:ForceChangeAfterDays > 0` AND `(now - baseline).TotalDays >= ForceChangeAfterDays` (default: 365)
   - `baseline = User.PasswordChangedAt ?? User.CreatedAt`

**When either condition is true:**
- Return `403 Forbidden` with error code `AUTH_FORCE_PASSWORD_CHANGE`
- Do NOT issue `accessToken` or `refreshToken`
- Do NOT update `LastLoginAt`

**Error responses (additions to LOGIN-1):**
| Condition | HTTP | Error Code |
|---|---|---|
| Forced-change required | 403 | `AUTH_FORCE_PASSWORD_CHANGE` |

#### Scenario: Force flag set explicitly

- GIVEN a user with `ForcePasswordChange = true`
- WHEN `POST /api/auth/login` is called with correct credentials
- THEN `403 Forbidden` is returned with error code `AUTH_FORCE_PASSWORD_CHANGE`
- AND no access token or refresh token is issued

#### Scenario: Password age exceeds policy

- GIVEN a user with `PasswordChangedAt = (now - 366 days)` and `ForceChangeAfterDays = 365`
- WHEN `POST /api/auth/login` is called with correct credentials
- THEN `403 Forbidden` is returned with error code `AUTH_FORCE_PASSWORD_CHANGE`
- AND no tokens are issued

#### Scenario: Password age within policy

- GIVEN a user with `PasswordChangedAt = (now - 364 days)` and `ForceChangeAfterDays = 365`
- WHEN `POST /api/auth/login` is called with correct credentials
- THEN `200 OK` is returned with access and refresh tokens (no forced-change)

#### Scenario: Baseline falls back to CreatedAt

- GIVEN a user with `PasswordChangedAt = null`, `CreatedAt = (now - 400 days)`, `ForceChangeAfterDays = 365`
- WHEN `POST /api/auth/login` is called with correct credentials
- THEN `403 Forbidden` is returned with error code `AUTH_FORCE_PASSWORD_CHANGE`

#### Scenario: ForceChangeAfterDays = 0 disables age check

- GIVEN `ForceChangeAfterDays = 0` in policy and a user with `PasswordChangedAt = null`, `CreatedAt = (now - 1000 days)`
- WHEN `POST /api/auth/login` is called with correct credentials
- THEN the age check is skipped; login succeeds if no other forced-change flag is set

---

## Password Management Data Model

### Requirement: REQ-PWD-6 — PasswordResetToken Entity

New table `PasswordResetTokens`:

| Column | Type | Constraints |
|---|---|---|
| `Id` | `Guid` | PRIMARY KEY |
| `UserId` | `Guid` | NOT NULL, FK → `Users.Id` |
| `TokenHash` | `string` | NOT NULL |
| `ExpiresAt` | `DateTime` (UTC) | NOT NULL |
| `UsedAt` | `DateTime?` (UTC) | NULL |
| `CreatedAt` | `DateTime` (UTC) | NOT NULL |

**Indexes:** `IX_PasswordResetTokens_UserId` on `UserId`.
**No unique index on `TokenHash`** — BCrypt hashes are not suited for DB index lookup; candidate tokens are found by `UserId` then verified via `BCrypt.Verify`.

### Requirement: REQ-PWD-7 — User Entity Additions

Four new columns added to the `Users` table:

| Column | Type | Default | Nullable |
|---|---|---|---|
| `FailedLoginAttempts` | `int` | `0` | NOT NULL |
| `LockoutUntil` | `datetime` | — | NULL |
| `PasswordChangedAt` | `datetime` | — | NULL |
| `ForcePasswordChange` | `bool` | `false` | NOT NULL |

---

## Password Management Policy Service

### Requirement: REQ-PWD-8 — IPasswordPolicyService

A new `IPasswordPolicyService` interface MUST exist in `SharedKernel/Services/` and be resolvable from DI. The concrete implementation reads the `PasswordPolicy` appsettings section.

**Interface contract:**

| Property | Type | Default |
|---|---|---|
| `MaxFailedAttempts` | `int` | `5` |
| `LockoutDurationMinutes` | `int` | `30` |
| `ForceChangeAfterDays` | `int` | `365` |
| `ResetTokenExpiryMinutes` | `int` | `30` |

**Configuration example (`appsettings.json`):**
```json
{
  "PasswordPolicy": {
    "MaxFailedAttempts": 5,
    "LockoutDurationMinutes": 30,
    "ForceChangeAfterDays": 365,
    "ResetTokenExpiryMinutes": 30
  }
}
```

All four values MUST fall back to the specified defaults when the `PasswordPolicy` section is absent or a key is missing.

---

## Password Management Security Audit Events

### Requirement: REQ-PWD-9 — New SecurityAuditLog Events

Two new event types MUST be written via the existing `ISecurityAuditWriter`:

**`PasswordChanged`**
- Written by: `ResetPassword` handler (on token-based reset) AND `ChangePassword` handler (on authenticated change)
- Required metadata: `UserId`, `IpAddress`, `UserAgent`

**`AccountLocked`**
- Written by: `LoginUser` handler when `FailedLoginAttempts` crosses the `MaxFailedAttempts` threshold
- Required metadata: `UserId`, `IpAddress`, `UserAgent`, `FailedAttempts` (count at lockout time)

Both events MUST follow the `SecurityAuditLog` schema already defined in the auth spec (EventType string, UserId, Timestamp, IpAddress, UserAgent).

---

## Password Management Frontend Requirements

### Requirement: REQ-PWD-FE-1 — ForgotPasswordView

A new public route `/forgot-password` MUST render a view with:
- An email input and a submit button
- On submit: calls `POST /api/auth/forgot-password`; on any response (success or error) shows a confirmation message (anti-enumeration: same UI regardless of result)
- No loading-error state that reveals whether email was found

**i18n keys:**
| Key | Purpose |
|---|---|
| `auth.password.forgotTitle` | Page/section heading |
| `auth.password.forgotDescription` | Instructions paragraph |
| `auth.password.emailLabel` | Email input label |
| `auth.password.sendLink` | Submit button label |
| `auth.password.linkSent` | Confirmation message shown after submit |

#### Scenario: Submit shows confirmation regardless of result

- GIVEN ForgotPasswordView is rendered
- WHEN a user submits an email address (registered or not)
- THEN the success/confirmation message (`auth.password.linkSent`) is displayed
- AND no error message differentiating registered vs. unregistered emails is shown

---

### Requirement: REQ-PWD-FE-2 — ResetPasswordView

A new public route `/reset-password` MUST read the `token` query parameter and render a view with:
- New password and confirm password inputs
- On submit: calls `POST /api/auth/reset-password` with `{ token, newPassword }`
- On `PWD_TOKEN_INVALID` or `PWD_TOKEN_EXPIRED`: shows an error state with a link back to `/forgot-password`
- On success: shows confirmation; user must navigate to `/login` manually (no auto-redirect to preserve security)

**i18n keys:**
| Key | Purpose |
|---|---|
| `auth.password.resetTitle` | Page heading |
| `auth.password.newPassword` | New password input label |
| `auth.password.confirmPassword` | Confirm password input label |
| `auth.password.resetSuccess` | Success confirmation message |
| `auth.password.tokenInvalid` | Error shown for `PWD_TOKEN_INVALID` |
| `auth.password.tokenExpired` | Error shown for `PWD_TOKEN_EXPIRED` |

#### Scenario: Valid token — reset succeeds

- GIVEN `/reset-password?token=abc123` is navigated to
- WHEN the user enters matching passwords and submits
- THEN `POST /api/auth/reset-password` is called
- AND on success the `auth.password.resetSuccess` message is displayed

#### Scenario: Expired token — error shown

- GIVEN the backend returns `PWD_TOKEN_EXPIRED`
- WHEN ResetPasswordView receives the error
- THEN an error message is displayed with a link navigating to `/forgot-password`

---

### Requirement: REQ-PWD-FE-3 — ChangePasswordModal

A new `ChangePasswordModal` component MUST be accessible from the `AppLayout` user dropdown menu. It renders:
- Current password, new password, and confirm password inputs
- On submit: calls `POST /api/auth/change-password` (authenticated)
- On success: shows inline confirmation; modal stays open briefly then closes; user remains logged in
- On `PWD_CURRENT_INCORRECT`: shows an inline error on the current password field
- On `AUTH_FORCE_PASSWORD_CHANGE` received at login (intercepted in auth store): user is redirected to `/change-password` (dedicated public-facing forced-change view) and cannot navigate to any authenticated route until the password has been changed

**i18n keys:**
| Key | Purpose |
|---|---|
| `auth.password.changeTitle` | Modal heading |
| `auth.password.currentPassword` | Current password input label |
| `auth.password.changeSuccess` | Success confirmation message |
| `auth.password.currentIncorrect` | Inline error for `PWD_CURRENT_INCORRECT` |

#### Scenario: Correct current password — change succeeds

- GIVEN an authenticated user opens ChangePasswordModal
- WHEN they enter the correct current password and a valid new password and submit
- THEN `200 OK` is returned
- AND the success message is shown
- AND the user remains authenticated (current session preserved)

#### Scenario: Wrong current password — inline error

- GIVEN an authenticated user opens ChangePasswordModal
- WHEN they enter an incorrect current password and submit
- THEN the `auth.password.currentIncorrect` message is shown inline on the current password field
- AND the form does not close

---

### Requirement: REQ-PWD-FE-4 — Auth Store Additions

The `auth.store.ts` Pinia store MUST expose three new actions:

| Action | Signature | Behavior |
|---|---|---|
| `requestPasswordReset` | `(email: string): Promise<void>` | Calls `POST /api/auth/forgot-password`; always resolves (no throw on 200) |
| `resetPassword` | `(token: string, newPassword: string): Promise<void>` | Calls `POST /api/auth/reset-password`; throws on `PWD_TOKEN_INVALID` / `PWD_TOKEN_EXPIRED` |
| `changePassword` | `(currentPassword: string, newPassword: string): Promise<void>` | Calls `POST /api/auth/change-password`; throws on `PWD_CURRENT_INCORRECT` |

---

### Requirement: REQ-PWD-FE-5 — Forced-Change Intercept

The `auth.store.ts` `login()` action MUST handle the `AUTH_FORCE_PASSWORD_CHANGE` error code:
- Store a reactive `forcePasswordChange` flag (boolean, initially `false`)
- On `AUTH_FORCE_PASSWORD_CHANGE` response: set flag to `true`, redirect to `/change-password`
- The router guard MUST prevent navigation to any `requiresAuth: true` route when `forcePasswordChange = true`, redirecting to `/change-password`
- The flag is cleared when `changePassword()` completes successfully

#### Scenario: Forced-change blocks all authenticated routes

- GIVEN `auth.store.forcePasswordChange = true`
- WHEN the user navigates to any `requiresAuth: true` route (e.g., `/budgets`)
- THEN the router redirects to `/change-password`
- AND the target route is NOT rendered

#### Scenario: Forced-change clears after successful change

- GIVEN `auth.store.forcePasswordChange = true`
- WHEN `changePassword()` completes successfully
- THEN `forcePasswordChange = false`
- AND the user can navigate to authenticated routes normally

---

## Password Management Shared Constraints

The following shared constraints apply to password management functionality:

- SC-PWD-1: All three new password endpoints (`forgot-password`, `reset-password`, `change-password`) MUST live under `/api/auth/` to match the existing auth prefix convention.
- SC-PWD-2: Password policy values MUST be configurable via `appsettings.json` under the `PasswordPolicy` section. Defaults apply when the section is absent.
- SC-PWD-3: The new EF Core migration (`AddPasswordManagement`) MUST be the only migration that adds `PasswordResetTokens` table and the four new `User` columns. Existing migrations MUST NOT be modified.
- SC-PWD-4: `newPassword` (in both `reset-password` and `change-password`) MUST be validated against the same rules as `password` in registration: Min 8 chars, max 72 chars, at least 1 uppercase, 1 lowercase, 1 digit.
- SC-PWD-5: All SecurityAuditLog writes (`PasswordChanged`, `AccountLocked`) MUST use the existing `ISecurityAuditWriter` interface already shipped with the `audit-log` change.
- SC-PWD-6: Reset token generation MUST use 64 cryptographically random bytes, stored as BCrypt hash (workFactor 6) — mirroring the `Invitation` pattern already in the codebase.

---

## Error Code Registry Extensions

The following error codes are added for password management:

| Code | HTTP | Meaning |
|---|---|---|
| `PWD_TOKEN_INVALID` | 404 | Reset token not found, already used, or hash mismatch |
| `PWD_TOKEN_EXPIRED` | 410 | Reset token exists but `ExpiresAt` is in the past |
| `PWD_CURRENT_INCORRECT` | 400 | `currentPassword` does not match stored BCrypt hash |
| `PWD_PASSWORD_TOO_WEAK` | 422 | `newPassword` does not meet complexity rules |
| `AUTH_ACCOUNT_LOCKED` | 423 | Account is locked due to repeated failed login attempts |
| `AUTH_FORCE_PASSWORD_CHANGE` | 403 | Login succeeded but password change is required before tokens are issued |
