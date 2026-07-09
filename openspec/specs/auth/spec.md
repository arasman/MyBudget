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

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Email already taken | 409 | `AUTH_EMAIL_TAKEN` |
| Password too weak | 422 | `AUTH_PASSWORD_TOO_WEAK` |
| Field missing/invalid | 422 | `FIELD_REQUIRED` / `FIELD_INVALID` |
| Locale not supported | 422 | `AUTH_LOCALE_UNSUPPORTED` |

**i18n keys (backend `.resx`):** `RegisterUser.EmailTaken`, `RegisterUser.PasswordTooWeak`, `RegisterUser.LocaleUnsupported`
**i18n keys (frontend `en.json` / `es.json`):** `auth.register.title`, `auth.register.emailPlaceholder`, `auth.register.passwordPlaceholder`, `auth.register.firstNamePlaceholder`, `auth.register.lastNamePlaceholder`, `auth.register.submit`, `auth.register.loginLink`, `auth.register.successMessage`

---

## Capability 2: User Login

### Requirement: LOGIN-1 — Credential Verification and Token Issuance

The system MUST verify credentials, issue a JWT access token (15-minute TTL) and a rotating refresh token (7-day TTL, single-use, hashed in DB), and update `LastLoginAt`.

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

The system MUST return the current user's profile. This is a read-only Dapper query — no EF Core. Requires valid JWT access token.

**Response:** `200 OK` with `{ id, email, firstName, lastName, preferredLocale, lastLoginAt, createdAt }`.

#### Scenario: Happy path

- GIVEN an authenticated user with a valid access token
- WHEN `GET /api/auth/me` is called with `Authorization: Bearer {accessToken}`
- THEN `200 OK` is returned with the user profile fields

#### Scenario: Expired access token

- GIVEN an expired JWT
- WHEN `GET /api/auth/me` is called
- THEN `401 Unauthorized` is returned (standard Bearer challenge)

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

The system MUST validate the invitation token (hash comparison), verify it is not expired or already used, create a `BudgetMembership` for the accepting user, and mark the invitation as used. If the user does not yet have an account, they MUST register first — the accept endpoint requires authentication.

#### Scenario: Happy path — authenticated user accepts

- GIVEN an authenticated user and a valid, non-expired, unused invitation token for their email
- WHEN `POST /api/auth/invitations/accept` is called with `{ token }` and a valid Bearer token
- THEN the `Invitation` record has `UsedAt` set to now
- AND a `BudgetMembership` is created for the user with the role from the invitation
- AND `200 OK` is returned with `{ budgetId, role }`

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

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Token not found | 404 | `AUTH_INVITATION_NOT_FOUND` |
| Token expired | 410 | `AUTH_INVITATION_EXPIRED` |
| Token already used | 410 | `AUTH_INVITATION_ALREADY_USED` |
| Email mismatch | 403 | `AUTH_INVITATION_EMAIL_MISMATCH` |
| Unauthenticated | 401 | (standard Bearer challenge) |

**i18n keys (backend `.resx`):** `AcceptInvitation.Expired`, `AcceptInvitation.AlreadyUsed`, `AcceptInvitation.NotFound`, `AcceptInvitation.EmailMismatch`
**i18n keys (frontend `en.json` / `es.json`):** `invitation.accept.title`, `invitation.accept.loading`, `invitation.accept.successMessage`, `invitation.accept.error.expired`, `invitation.accept.error.alreadyUsed`, `invitation.accept.error.mismatch`

---

## Capability 8: Per-Budget Authorization

### Requirement: AUTHZ-1 — Role Resolution at Request Time

The system MUST resolve the current user's role for a given budget by querying `BudgetMembership` at request time. This MUST NOT rely on any role stored in the JWT.

**Role hierarchy (descending privilege):** `owner` > `admin` > `operator` > `read-only`

**Caching:** The authorization handler SHOULD use a short-TTL in-memory cache (keyed by `userId + budgetId`, TTL ≤ 60 seconds) to avoid N+1 DB queries per request. Cache MUST be invalidated when membership changes.

#### Scenario: Authorized request

- GIVEN user has `admin` role in budget `{id}` (from `BudgetMembership`)
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

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Role below required threshold | 403 | `AUTH_INSUFFICIENT_ROLE` |
| No membership for budget | 403 | `AUTH_NOT_A_MEMBER` |

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
