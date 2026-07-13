# Password Management — Delta Spec

## Purpose

This spec defines what the system MUST do after the `password-management` change is applied.
It is additive to the existing `auth` spec. Requirements here describe observable behavior — not implementation.

---

## Shared Constraints

- SC-PWD-1: All three new password endpoints (`forgot-password`, `reset-password`, `change-password`) MUST live under `/api/auth/` to match the existing auth prefix convention.
- SC-PWD-2: Password policy values MUST be configurable via `appsettings.json` under the `PasswordPolicy` section. Defaults apply when the section is absent.
- SC-PWD-3: The new EF Core migration (`AddPasswordManagement`) MUST be the only migration that adds `PasswordResetTokens` table and the four new `User` columns. Existing migrations MUST NOT be modified.
- SC-PWD-4: `newPassword` (in both `reset-password` and `change-password`) MUST be validated against the same rules as `password` in registration: Min 8 chars, max 72 chars, at least 1 uppercase, 1 lowercase, 1 digit.
- SC-PWD-5: All SecurityAuditLog writes (`PasswordChanged`, `AccountLocked`) MUST use the existing `ISecurityAuditWriter` interface already shipped with the `audit-log` change.
- SC-PWD-6: Reset token generation MUST use 64 cryptographically random bytes, stored as BCrypt hash (workFactor 6) — mirroring the `Invitation` pattern already in the codebase.

---

## Capability: Password Recovery

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

## Capability: Authenticated Password Change

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

## Capability: Login Lockout (LoginUser modification)

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

## Capability: Forced Password Change (LoginUser modification)

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

## Data Model Delta

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

## Policy Service

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

## Security Audit Events

### Requirement: REQ-PWD-9 — New SecurityAuditLog Events

Two new event types MUST be written via the existing `ISecurityAuditWriter`:

**`PasswordChanged`**
- Written by: `ResetPassword` handler (on token-based reset) AND `ChangePassword` handler (on authenticated change)
- Required metadata: `UserId`, `IpAddress`, `UserAgent`

**`AccountLocked`**
- Written by: `LoginUser` handler when `FailedLoginAttempts` crosses the `MaxFailedAttempts` threshold
- Required metadata: `UserId`, `IpAddress`, `UserAgent`, `FailedAttempts` (count at lockout time)

Both events MUST follow the `SecurityAuditLog` schema already defined in the `audit-log` spec (EventType string, UserId, Timestamp, IpAddress, UserAgent).

---

## Frontend Requirements

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

## Test Requirements

### REQ-PWD-TEST-1 — Backend Unit Tests

| Test | What is verified |
|---|---|
| `AppSettingsPasswordPolicyService` — all defaults | Returns correct defaults when `PasswordPolicy` section is absent |
| `AppSettingsPasswordPolicyService` — section overrides | Returns configured values when section is present |
| `User.RecordFailedLogin` — below threshold | Increments `FailedLoginAttempts`; does NOT set `LockoutUntil` |
| `User.RecordFailedLogin` — at threshold | Sets `LockoutUntil`; sets `FailedLoginAttempts = MaxFailedAttempts` |
| `User.UpdatePassword` — clears flags | Sets `PasswordHash`, `PasswordChangedAt`, `ForcePasswordChange = false`, `FailedLoginAttempts = 0`, `LockoutUntil = null` |

### REQ-PWD-TEST-2 — Backend Integration Tests

| Test | Endpoint | Scenario |
|---|---|---|
| Happy path — registered email | `POST /api/auth/forgot-password` | `PasswordResetToken` created, email queued |
| No-op — unknown email | `POST /api/auth/forgot-password` | `200 OK`, no token, no email |
| Happy path | `POST /api/auth/reset-password` | Password updated, token used, refresh tokens revoked |
| Expired token | `POST /api/auth/reset-password` | `410 PWD_TOKEN_EXPIRED` |
| Invalid token | `POST /api/auth/reset-password` | `404 PWD_TOKEN_INVALID` |
| Happy path | `POST /api/auth/change-password` | Password updated, other tokens revoked, current preserved |
| Wrong current password | `POST /api/auth/change-password` | `400 PWD_CURRENT_INCORRECT` |
| Lockout sequence (5 failures → locked → reset clears) | `POST /api/auth/login` × N + reset | Full lockout + recovery flow |
| Forced-change by age | `POST /api/auth/login` | `403 AUTH_FORCE_PASSWORD_CHANGE` when age >= threshold |
| Forced-change clears after change-password | `POST /api/auth/change-password` | Subsequent login succeeds |

### REQ-PWD-TEST-3 — Frontend Vitest

| Test | Component / Store |
|---|---|
| ForgotPasswordView renders correctly | Renders email input and submit button |
| ForgotPasswordView shows confirmation after submit | `linkSent` message visible after any submit outcome |
| ResetPasswordView — shows token-expired error state | Error message and link to `/forgot-password` visible |
| ResetPasswordView — password mismatch validation | Inline error when `newPassword !== confirmPassword` |
| ChangePasswordModal — `PWD_CURRENT_INCORRECT` shows inline error | Error appears on current password field |
| auth.store — `requestPasswordReset` resolves on 200 | Resolves without throw |
| auth.store — `forcePasswordChange` flag blocks navigation | Router redirects when flag is true |

### REQ-PWD-TEST-4 — E2E Playwright

| Test | Flow |
|---|---|
| Full forgot-password → reset flow | Navigate to `/forgot-password` → submit email → receive link in Mailpit → navigate to reset URL → set new password → login with new password succeeds |
| Change-password from settings | Login → open user dropdown → open ChangePasswordModal → change password → confirm success → stay logged in |
| Lockout after N failures | Login with wrong password N times → verify `AUTH_ACCOUNT_LOCKED` response → reset password clears lockout → login succeeds |
| Forced-change blocks navigation | Login returns `AUTH_FORCE_PASSWORD_CHANGE` → verify redirect to `/change-password` → verify authenticated routes inaccessible → complete change → verify normal navigation |

---

## Error Code Registry (new codes)

| Code | HTTP | Meaning |
|---|---|---|
| `PWD_TOKEN_INVALID` | 404 | Reset token not found, already used, or hash mismatch |
| `PWD_TOKEN_EXPIRED` | 410 | Reset token exists but `ExpiresAt` is in the past |
| `PWD_CURRENT_INCORRECT` | 400 | `currentPassword` does not match stored BCrypt hash |
| `PWD_PASSWORD_TOO_WEAK` | 422 | `newPassword` does not meet complexity rules |
| `AUTH_ACCOUNT_LOCKED` | 423 | Account is locked due to repeated failed login attempts |
| `AUTH_FORCE_PASSWORD_CHANGE` | 403 | Login succeeded but password change is required before tokens are issued |

---

## i18n Key Registry

### Backend `.resx` keys

| Key | Handler |
|---|---|
| `RequestPasswordReset.EmailQueued` | ForgotPassword handler (success log message) |
| `ResetPassword.TokenInvalid` | ResetPassword handler |
| `ResetPassword.TokenExpired` | ResetPassword handler |
| `ResetPassword.PasswordTooWeak` | ResetPassword validator |
| `ChangePassword.CurrentIncorrect` | ChangePassword handler |
| `ChangePassword.PasswordTooWeak` | ChangePassword validator |
| `LoginUser.AccountLocked` | LoginUser handler (new lockout path) |
| `LoginUser.ForcePasswordChange` | LoginUser handler (new forced-change path) |
| `Email.PasswordReset.Subject` | Reset email subject |
| `Email.PasswordReset.Body` | Reset email HTML body template |

### Frontend `en.json` / `es.json` keys

| Key | Used In |
|---|---|
| `auth.password.forgotTitle` | ForgotPasswordView |
| `auth.password.forgotDescription` | ForgotPasswordView |
| `auth.password.emailLabel` | ForgotPasswordView |
| `auth.password.sendLink` | ForgotPasswordView |
| `auth.password.linkSent` | ForgotPasswordView |
| `auth.password.resetTitle` | ResetPasswordView |
| `auth.password.newPassword` | ResetPasswordView + ChangePasswordModal |
| `auth.password.confirmPassword` | ResetPasswordView + ChangePasswordModal |
| `auth.password.resetSuccess` | ResetPasswordView |
| `auth.password.tokenInvalid` | ResetPasswordView |
| `auth.password.tokenExpired` | ResetPasswordView |
| `auth.password.changeTitle` | ChangePasswordModal |
| `auth.password.currentPassword` | ChangePasswordModal |
| `auth.password.changeSuccess` | ChangePasswordModal |
| `auth.password.currentIncorrect` | ChangePasswordModal |
| `auth.login.error.accountLocked` | LoginView |
| `auth.login.error.forcePasswordChange` | LoginView / auth store intercept |
