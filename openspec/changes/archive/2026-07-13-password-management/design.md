# Design: Password Management

## Technical Approach

Three new VSA slices (RequestPasswordReset, ResetPassword, ChangePassword) plus modifications to LoginUserHandler for lockout and forced-change enforcement. A new `PasswordResetToken` entity mirrors the existing `Invitation` BCrypt-token pattern. `IPasswordPolicyService` follows the `IAuditRetentionPolicy` appsettings-backed pattern. Refresh token invalidation uses direct EF `DbSet<RefreshToken>` queries. Frontend adds 2 public views, 1 modal, 3 store actions, and i18n keys.

## Architecture Decisions

### ADR-001: PasswordResetToken as standalone table

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Standalone table (mirrors Invitation) | One more table; clean semantics, rollback | **Chosen** |
| Reuse RefreshTokens with discriminator | No new table; mixed semantics, violates SRP | Rejected |
| Stateless JWT reset token | No DB write; cannot invalidate mid-flight | Rejected |

**Rationale**: Exact pattern match with `Invitation`. Independent revocability, clean rollback (drop table).

### ADR-002: Lockout check before BCrypt.Verify

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Check lockout BEFORE BCrypt | Early exit avoids timing leak; no BCrypt on locked accounts | **Chosen** |
| Check after BCrypt | Simpler flow; timing inconsistency on locked vs unlocked | Rejected |

**Rationale**: Security — prevents timing side-channel that leaks account-exists info.

### ADR-003: Refresh token invalidation via EF DbSet

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Direct `_db.RefreshTokens.Where(...).ExecuteUpdateAsync` | No new repository; uses existing DbSet | **Chosen** |
| New `IRefreshTokenRepository` | Cleaner interface; over-abstraction for 2 call sites | Rejected |

**Rationale**: `RefreshToken` entity already has `Revoke()` method. Bulk revocation via `ExecuteUpdateAsync` is efficient and idiomatic. Two call sites (ResetPassword, ChangePassword) do not justify a repository.

### ADR-004: Forced-change returns error code, no JWT issued

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Return `AUTH_FORCE_PASSWORD_CHANGE` error, no tokens | Frontend must redirect to change-password; no session leakage | **Chosen** |
| Issue short-lived token with `force_change` claim | User gets partial session; complexity in claim checking | Rejected |

**Rationale**: No token = no session = no risk of using the app without changing password. Frontend catches the error code and redirects to `/forgot-password` (since user has no session, they must use the reset flow).

## Data Flow

### Password Reset Flow
```
User ──POST /forgot-password──> RequestPasswordResetHandler
    │                               │ Dapper: find user by email
    │                               │ Generate 32-byte random token
    │                               │ BCrypt hash (wf:6) → save PasswordResetToken
    │                               │ IEmailSender.SendAsync(reset link)
    │                               └──> Always 200 (anti-enumeration)
    │
User ──POST /reset-password──> ResetPasswordHandler
    │                               │ EF: load unexpired/unused tokens for email
    │                               │ BCrypt.Verify each token against raw
    │                               │ Update User.PasswordHash, clear lockout/force-change
    │                               │ Revoke ALL refresh tokens for user
    │                               │ Mark token UsedAt
    │                               │ ISecurityAuditWriter → PasswordChanged
    │                               └──> 200 OK
```

### Login with Lockout + Forced-Change
```
LoginUserHandler.Handle:
    1. Dapper SELECT (add FailedLoginAttempts, LockoutUntil, PasswordChangedAt, ForcePasswordChange)
    2. IF LockoutUntil > UtcNow → return AUTH_ACCOUNT_LOCKED (no BCrypt)
    3. BCrypt.Verify
    4. IF fail → EF: RecordFailedLogin(); if threshold → AccountLocked audit; save; return AUTH_INVALID_CREDENTIALS
    5. IF pass → EF: RecordSuccessfulLogin() (reset counters)
    6. Check forced-change: (PasswordChangedAt ?? CreatedAt) + ForceChangeAfterDays < UtcNow OR ForcePasswordChange == true
    7. IF forced → return AUTH_FORCE_PASSWORD_CHANGE (no tokens issued)
    8. Normal flow: issue JWT + refresh token
```

## Interfaces / Contracts

```csharp
// SharedKernel/Services/IPasswordPolicyService.cs
public interface IPasswordPolicyService
{
    int MaxFailedAttempts { get; }        // default: 5
    int LockoutMinutes { get; }           // default: 15
    int ForceChangeAfterDays { get; }     // default: 365
    int ResetTokenExpiryMinutes { get; }  // default: 30
}
```

```csharp
// User.cs — new fields and methods
public int FailedLoginAttempts { get; private set; }
public DateTime? LockoutUntil { get; private set; }
public DateTime? PasswordChangedAt { get; private set; }
public bool ForcePasswordChange { get; private set; }

public bool IsLockedOut => LockoutUntil.HasValue && DateTime.UtcNow < LockoutUntil.Value;

public void RecordFailedLogin(int maxAttempts, int lockoutMinutes) {
    FailedLoginAttempts++;
    if (FailedLoginAttempts >= maxAttempts)
        LockoutUntil = DateTime.UtcNow.AddMinutes(lockoutMinutes);
    UpdatedAt = DateTimeOffset.UtcNow;
}

public void RecordSuccessfulLogin() {
    FailedLoginAttempts = 0;
    LockoutUntil = null;
    LastLoginAt = DateTime.UtcNow;
    UpdatedAt = DateTimeOffset.UtcNow;
}

public void UpdatePassword(string newHash) {
    PasswordHash = newHash;
    PasswordChangedAt = DateTime.UtcNow;
    ForcePasswordChange = false;
    FailedLoginAttempts = 0;
    LockoutUntil = null;
    UpdatedAt = DateTimeOffset.UtcNow;
}
```

```csharp
// PasswordResetToken entity — mirrors Invitation
public sealed class PasswordResetToken : BaseEntity {
    public Guid UserId { get; private set; }
    public string TokenHash { get; private set; }  // BCrypt, wf:6
    public DateTime ExpiresAt { get; private set; }
    public DateTime? UsedAt { get; private set; }
    public User? User { get; private set; }         // navigation

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsUsed => UsedAt.HasValue;
    public void MarkUsed() { UsedAt = DateTime.UtcNow; UpdatedAt = DateTimeOffset.UtcNow; }
}
```

```json
// appsettings.json — PasswordPolicy section
{
  "PasswordPolicy": {
    "MaxFailedAttempts": 5,
    "LockoutMinutes": 15,
    "ForceChangeAfterDays": 365,
    "ResetTokenExpiryMinutes": 30
  }
}
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SharedKernel/Entities/User.cs` | Modify | 4 new fields, `IsLockedOut`, `RecordFailedLogin`, `RecordSuccessfulLogin`, `UpdatePassword` |
| `SharedKernel/Entities/PasswordResetToken.cs` | Create | Entity mirroring Invitation pattern |
| `SharedKernel/Persistence/AppDbContext.cs` | Modify | Add `DbSet<PasswordResetToken>` |
| `SharedKernel/Persistence/Configurations/PasswordResetTokenConfiguration.cs` | Create | PK, FK to Users, unique IX on TokenHash, MaxLength(72) |
| `SharedKernel/Persistence/Configurations/UserConfiguration.cs` | Modify | 4 new column configs (defaults: 0, null, null, false) |
| `SharedKernel/Services/IPasswordPolicyService.cs` | Create | Interface with 4 properties |
| `SharedKernel/Services/AppSettingsPasswordPolicyService.cs` | Create | `IOptions<PasswordPolicyOptions>` backed |
| `Extensions/ServiceCollectionExtensions.cs` | Modify | Register `IPasswordPolicyService`, bind `PasswordPolicy` section |
| `Features/Auth/LoginUser/LoginUserHandler.cs` | Modify | Add lockout check (before BCrypt), failed-login recording, forced-change check |
| `Features/Auth/LoginUser/LoginUserCommand.cs` | — | No change (same contract) |
| `Features/Auth/RequestPasswordReset/` (4 files) | Create | POST `/api/auth/forgot-password`, anonymous |
| `Features/Auth/ResetPassword/` (4 files) | Create | POST `/api/auth/reset-password`, anonymous |
| `Features/Auth/ChangePassword/` (4 files) | Create | POST `/api/auth/change-password`, authenticated |
| `Migrations/*_AddPasswordManagement.cs` | Create | ALTER Users + CREATE PasswordResetTokens |
| `appsettings.json` | Modify | Add `PasswordPolicy` section |
| `frontend/src/stores/auth.store.ts` | Modify | Add `requestPasswordReset`, `resetPassword`, `changePassword` |
| `frontend/src/router/index.ts` | Modify | Add `/forgot-password`, `/reset-password` public routes |
| `frontend/src/views/ForgotPasswordView.vue` | Create | Email form, success message |
| `frontend/src/views/ResetPasswordView.vue` | Create | Token from query param, new password form |
| `frontend/src/components/auth/ChangePasswordModal.vue` | Create | daisyUI modal with current + new password |
| `frontend/src/layouts/AppLayout.vue` | Modify | Add "Change Password" to user dropdown |
| `frontend/src/i18n/locales/en.json` | Modify | Add `auth.password.*` keys |
| `frontend/src/i18n/locales/es.json` | Modify | Add `auth.password.*` keys |

## Slice Design

### RequestPasswordResetHandler
- **Endpoint**: `POST /api/auth/forgot-password` — anonymous, always 200
- **Dependencies**: `ConnectionFactory`, `AppDbContext`, `IPasswordPolicyService`, `IEmailSender`, `IConfiguration`, `ILogger`
- **Logic**: (1) Dapper find user by `Email` (using `ConnectionFactory`); (2) if not found → return 200 (silent); (3) generate 32-byte random token (`RandomNumberGenerator.GetBytes(32)` → Base64Url); (4) BCrypt hash (wf:6); (5) save `PasswordResetToken` via EF; (6) build reset link: `{App:FrontendBaseUrl}/reset-password?token={rawToken}&email={email}`; (7) `IEmailSender.SendAsync`; (8) return 200
- **Email**: Subject: "Reset your MyBudget password". Body: link with 30-min expiry note.

### ResetPasswordHandler
- **Endpoint**: `POST /api/auth/reset-password` — anonymous
- **Command**: `{ Email, Token, NewPassword }`
- **Dependencies**: `AppDbContext`, `ISecurityAuditWriter`, `ILogger`
- **Logic**: (1) EF load unexpired, unused `PasswordResetTokens` for email (via User nav); (2) BCrypt.Verify each against raw token; (3) if no match → `RESET_TOKEN_INVALID`; (4) `user.UpdatePassword(BCrypt.HashPassword(newPassword, wf:12))`; (5) `token.MarkUsed()`; (6) revoke ALL refresh tokens: `_db.RefreshTokens.Where(rt => rt.UserId == user.Id && rt.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow))`; (7) `SaveChangesAsync`; (8) audit `PasswordChanged`; (9) return 200

### ChangePasswordHandler
- **Endpoint**: `POST /api/auth/change-password` — `RequireAuthorization()`
- **Command**: `{ CurrentPassword, NewPassword, CurrentRefreshToken }`
- **Dependencies**: `AppDbContext`, `ICurrentUserService`, `ISecurityAuditWriter`, `ILogger`
- **Logic**: (1) load user by `ICurrentUserService.UserId`; (2) BCrypt.Verify `CurrentPassword` against stored hash; (3) if fail → `AUTH_INVALID_CREDENTIALS`; (4) `user.UpdatePassword(BCrypt.HashPassword(newPassword, wf:12))`; (5) revoke all refresh tokens EXCEPT current: `_db.RefreshTokens.Where(rt => rt.UserId == userId && rt.RevokedAt == null).ToListAsync()` → for each, BCrypt.Verify against `CurrentRefreshToken` — if match, skip; else `rt.Revoke()`; (6) `SaveChangesAsync`; (7) audit `PasswordChanged`; (8) return 200

### LoginUserHandler Modifications
- **Insert at top of Handle** (before existing BCrypt.Verify block):
  - Extend Dapper SELECT to include `"FailedLoginAttempts", "LockoutUntil", "PasswordChangedAt", "ForcePasswordChange"`
  - Add `FailedLoginAttempts`, `LockoutUntil`, `PasswordChangedAt`, `ForcePasswordChange` to `UserRow` record
  - After Dapper query, before BCrypt: `if (row?.LockoutUntil > DateTime.UtcNow) → audit FailedLogin → return AUTH_ACCOUNT_LOCKED`
- **Modify failed-login path**: load user via EF, call `user.RecordFailedLogin(policy.MaxFailedAttempts, policy.LockoutMinutes)`, if `user.IsLockedOut` → audit `AccountLocked`, `SaveChangesAsync`
- **After successful BCrypt + RecordSuccessfulLogin**: check `(row.PasswordChangedAt ?? user.CreatedAt) + policy.ForceChangeAfterDays < UtcNow || row.ForcePasswordChange` → return `AUTH_FORCE_PASSWORD_CHANGE`
- **New dependency**: inject `IPasswordPolicyService`

## Refresh Token Invalidation

- **Current storage**: `RefreshTokens` table, `RefreshToken` entity with `RevokedAt` nullable + `Revoke()` method
- **"Invalidate all"** (reset-password): `ExecuteUpdateAsync` setting `RevokedAt = DateTime.UtcNow` on all active tokens for user — no need to load entities
- **"Invalidate all except current"** (change-password): Load active tokens, BCrypt.Verify each against the `CurrentRefreshToken` passed by the frontend; skip the match, revoke the rest. This is O(N) BCrypt verifications where N = active refresh tokens per user (typically 1-5).

## Frontend Design

### auth.store.ts — new actions
```typescript
async function requestPasswordReset(email: string): Promise<void> {
  await http.post('/api/auth/forgot-password', { email })
}

async function resetPassword(email: string, token: string, newPassword: string): Promise<void> {
  await http.post('/api/auth/reset-password', { email, token, newPassword })
}

async function changePassword(currentPassword: string, newPassword: string): Promise<void> {
  const currentRefreshToken = localStorage.getItem('refreshToken')
  await http.post('/api/auth/change-password', { currentPassword, newPassword, currentRefreshToken })
}
```

### Router — new routes
```typescript
{ path: '/forgot-password', component: PublicLayout, children: [
    { path: '', name: 'ForgotPassword', component: () => import('@/views/ForgotPasswordView.vue'), meta: { public: true } }
]},
{ path: '/reset-password', component: PublicLayout, children: [
    { path: '', name: 'ResetPassword', component: () => import('@/views/ResetPasswordView.vue'), meta: { public: true } }
]},
```

### Forced-change intercept
In `auth.store.login()`: catch error, if `response.data` contains `AUTH_FORCE_PASSWORD_CHANGE`, redirect to `/forgot-password` with a query param `?reason=forced`. The ForgotPasswordView shows contextual messaging when `reason=forced`.

### ChangePasswordModal integration
In `AppLayout.vue` user dropdown, add `<li><button @click="showChangePassword = true">{{ $t('auth.password.changeLabel') }}</button></li>` before the Logout button. `ChangePasswordModal` is a daisyUI `<dialog>` component imported in AppLayout.

## Migration Design

**Migration name**: `AddPasswordManagement`

```sql
ALTER TABLE "Users" ADD COLUMN "FailedLoginAttempts" integer NOT NULL DEFAULT 0;
ALTER TABLE "Users" ADD COLUMN "LockoutUntil" timestamp without time zone NULL;
ALTER TABLE "Users" ADD COLUMN "PasswordChangedAt" timestamp without time zone NULL;
ALTER TABLE "Users" ADD COLUMN "ForcePasswordChange" boolean NOT NULL DEFAULT false;

CREATE TABLE "PasswordResetTokens" (
    "Id" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "TokenHash" character varying(72) NOT NULL,
    "ExpiresAt" timestamp without time zone NOT NULL,
    "UsedAt" timestamp without time zone NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    CONSTRAINT "PK_PasswordResetTokens" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_PasswordResetTokens_Users_UserId" FOREIGN KEY ("UserId")
        REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_PasswordResetTokens_TokenHash" ON "PasswordResetTokens" ("TokenHash");
CREATE INDEX "IX_PasswordResetTokens_UserId" ON "PasswordResetTokens" ("UserId");
```

## PR Delivery Map

### PR1 (~300 lines) — Foundation + LoginUser enforcement
| File | Action |
|------|--------|
| `SharedKernel/Entities/User.cs` | Modify |
| `SharedKernel/Entities/PasswordResetToken.cs` | Create |
| `SharedKernel/Persistence/AppDbContext.cs` | Modify |
| `SharedKernel/Persistence/Configurations/PasswordResetTokenConfiguration.cs` | Create |
| `SharedKernel/Persistence/Configurations/UserConfiguration.cs` | Modify |
| `SharedKernel/Services/IPasswordPolicyService.cs` | Create |
| `SharedKernel/Services/AppSettingsPasswordPolicyService.cs` | Create |
| `Extensions/ServiceCollectionExtensions.cs` | Modify |
| `Features/Auth/LoginUser/LoginUserHandler.cs` | Modify |
| `Migrations/*_AddPasswordManagement.cs` | Create |
| `appsettings.json` | Modify |
| Unit tests for User domain methods + LoginUser lockout/forced-change | Create |

### PR2 (~300 lines) — Password slices
| File | Action |
|------|--------|
| `Features/Auth/RequestPasswordReset/` (4 files) | Create |
| `Features/Auth/ResetPassword/` (4 files) | Create |
| `Features/Auth/ChangePassword/` (4 files) | Create |
| Unit + integration tests for all 3 slices | Create |

### PR3 (~250 lines) — Frontend
| File | Action |
|------|--------|
| `frontend/src/stores/auth.store.ts` | Modify |
| `frontend/src/router/index.ts` | Modify |
| `frontend/src/views/ForgotPasswordView.vue` | Create |
| `frontend/src/views/ResetPasswordView.vue` | Create |
| `frontend/src/components/auth/ChangePasswordModal.vue` | Create |
| `frontend/src/layouts/AppLayout.vue` | Modify |
| `frontend/src/i18n/locales/en.json` | Modify |
| `frontend/src/i18n/locales/es.json` | Modify |
| Vitest tests for store actions | Create |

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | User.RecordFailedLogin, RecordSuccessfulLogin, UpdatePassword, IsLockedOut | In-memory, no DB |
| Unit | PasswordPolicyService defaults + overrides | Mock IOptions |
| Unit | LoginUserHandler lockout/forced-change branches | NSubstitute mocks |
| Unit | RequestPasswordResetHandler anti-enumeration (unknown email) | NSubstitute mocks |
| Unit | ResetPasswordHandler token validation + expiry | SQLite in-memory |
| Unit | ChangePasswordHandler current-password verification | SQLite in-memory |
| Integration | Full HTTP round-trip for all 3 new endpoints + lockout scenario | WebApplicationFactory |
| Vitest | auth.store actions (requestPasswordReset, resetPassword, changePassword) | vi.mock axios |
| E2E | Forgot → reset flow; change-password from settings | Playwright |

## Open Questions

- None. All open questions from proposal have been resolved per orchestrator decisions.
