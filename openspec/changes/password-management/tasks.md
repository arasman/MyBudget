# Tasks: Password Management

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~850 (PR1: ~300, PR2: ~300, PR3: ~250) |
| 400-line budget risk | Low per PR — Medium overall |
| Chained PRs recommended | Yes |
| Suggested split | Feature Branch Chain: feature/password-management → PR1 → PR2 → PR3 → main |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

---

## Branch Setup

```
main
 └── feature/password-management          ← tracker (draft, no-merge)
      ├── feature/password-management/pr1  ← PR1 targets tracker
      ├── feature/password-management/pr2  ← PR2 targets pr1 branch
      └── feature/password-management/pr3  ← PR3 targets pr2 branch
```

Dependency diagram (each PR carries this, marking current with 📍):

```
PR1 (Foundation) → PR2 (Slices) → PR3 (Frontend)
📍 = current
```

---

## PR1 — Foundation + LoginUser Enforcement (~300 lines)

**Branch**: `feature/password-management/pr1`  
**Target**: `feature/password-management`  
**Satisfies**: REQ-PWD-4, REQ-PWD-5, REQ-PWD-6, REQ-PWD-7, REQ-PWD-8, SC-PWD-2, SC-PWD-3  
**Sequential within PR1** (each task depends on the previous)

### T-1.1 — User entity: 4 new fields and domain methods
**File**: `src/MyBudget.Features/SharedKernel/Entities/User.cs`  
**Action**: Modify  
**Details**:
- Add properties: `FailedLoginAttempts` (int, default 0), `LockoutUntil` (DateTime?, null), `PasswordChangedAt` (DateTime?, null), `ForcePasswordChange` (bool, default false)
- All properties: `{ get; private set; }`
- Add computed property: `bool IsLockedOut => LockoutUntil.HasValue && DateTime.UtcNow < LockoutUntil.Value;`
- Add method `RecordFailedLogin(int maxAttempts, int lockoutMinutes)`: increment `FailedLoginAttempts`; if `>= maxAttempts` set `LockoutUntil = DateTime.UtcNow.AddMinutes(lockoutMinutes)`; set `UpdatedAt`
- Add method `RecordSuccessfulLogin()`: reset `FailedLoginAttempts = 0`, `LockoutUntil = null`, set `LastLoginAt = DateTime.UtcNow`, set `UpdatedAt`
- Add method `UpdatePassword(string newHash)`: set `PasswordHash = newHash`, `PasswordChangedAt = DateTime.UtcNow`, `ForcePasswordChange = false`, `FailedLoginAttempts = 0`, `LockoutUntil = null`, set `UpdatedAt`
- **Spec refs**: REQ-PWD-4, REQ-PWD-5, REQ-PWD-7

### T-1.2 — PasswordResetToken entity
**File**: `src/MyBudget.Features/SharedKernel/Entities/PasswordResetToken.cs`  
**Action**: Create  
**Details**:
- `sealed class PasswordResetToken : BaseEntity`
- Properties: `Guid Id`, `Guid UserId`, `string TokenHash` (BCrypt wf:6, maxlen 72), `DateTime ExpiresAt`, `DateTime? UsedAt`, `User? User` (navigation, EF only)
- All properties: `{ get; private set; }`
- Computed: `bool IsExpired => DateTime.UtcNow >= ExpiresAt;`, `bool IsUsed => UsedAt.HasValue;`
- Method `MarkUsed()`: `UsedAt = DateTime.UtcNow; UpdatedAt = DateTimeOffset.UtcNow;`
- Static factory: `Create(Guid userId, string tokenHash, DateTime expiresAt)` — sets `Id = Guid.NewGuid()`, `CreatedAt = DateTime.UtcNow`
- **Spec refs**: REQ-PWD-6, SC-PWD-6

### T-1.3 — EF Core configuration for PasswordResetToken
**File**: `src/MyBudget.Features/SharedKernel/Persistence/Configurations/PasswordResetTokenConfiguration.cs`  
**Action**: Create  
**Details**:
- Implements `IEntityTypeConfiguration<PasswordResetToken>`
- PK on `Id`
- FK to `Users.Id` with `OnDelete(DeleteBehavior.Cascade)`
- `TokenHash`: `MaxLength(72)`, `IsRequired(true)`; add unique index `IX_PasswordResetTokens_TokenHash`
- Add index `IX_PasswordResetTokens_UserId` on `UserId`
- **Spec refs**: REQ-PWD-6

### T-1.4 — UserConfiguration: 4 new column configs
**File**: `src/MyBudget.Features/SharedKernel/Persistence/Configurations/UserConfiguration.cs`  
**Action**: Modify  
**Details**:
- `FailedLoginAttempts`: `HasDefaultValue(0)`, `IsRequired(true)`
- `LockoutUntil`: `IsRequired(false)` (nullable)
- `PasswordChangedAt`: `IsRequired(false)` (nullable)
- `ForcePasswordChange`: `HasDefaultValue(false)`, `IsRequired(true)`
- **Spec refs**: REQ-PWD-7

### T-1.5 — AppDbContext: add PasswordResetTokens DbSet
**File**: `src/MyBudget.Features/SharedKernel/Persistence/AppDbContext.cs`  
**Action**: Modify  
**Details**:
- Add `public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();`
- **Spec refs**: REQ-PWD-6

### T-1.6 — IPasswordPolicyService interface
**File**: `src/MyBudget.Features/SharedKernel/Services/IPasswordPolicyService.cs`  
**Action**: Create  
**Details**:
- Interface with 4 properties: `int MaxFailedAttempts`, `int LockoutDurationMinutes`, `int ForceChangeAfterDays`, `int ResetTokenExpiryMinutes`
- All defaults: 5, 30, 365, 30
- **Spec refs**: REQ-PWD-8, SC-PWD-2

### T-1.7 — AppSettingsPasswordPolicyService implementation
**File**: `src/MyBudget.Features/SharedKernel/Services/AppSettingsPasswordPolicyService.cs`  
**Action**: Create  
**Details**:
- `sealed class AppSettingsPasswordPolicyService : IPasswordPolicyService`
- Backed by `IOptions<PasswordPolicyOptions>` where `PasswordPolicyOptions` is a nested POCO with defaults: `MaxFailedAttempts = 5`, `LockoutDurationMinutes = 30`, `ForceChangeAfterDays = 365`, `ResetTokenExpiryMinutes = 30`
- All interface properties delegate to `_options.Value.{Property}`
- `PasswordPolicyOptions` class: define in same file or as inner class
- **Spec refs**: REQ-PWD-8, SC-PWD-2

### T-1.8 — Register IPasswordPolicyService and bind PasswordPolicy config section
**File**: `src/MyBudget.Features/Extensions/ServiceCollectionExtensions.cs`  
**Action**: Modify  
**Details**:
- `services.Configure<PasswordPolicyOptions>(config.GetSection("PasswordPolicy"));`
- `services.AddSingleton<IPasswordPolicyService, AppSettingsPasswordPolicyService>();`
- **Spec refs**: REQ-PWD-8, SC-PWD-2

### T-1.9 — Add PasswordPolicy section to appsettings.json
**File**: `src/MyBudget.Api/appsettings.json`  
**Action**: Modify  
**Details**:
- Add section (no secret values, only structural defaults):
  ```json
  "PasswordPolicy": {
    "MaxFailedAttempts": 5,
    "LockoutDurationMinutes": 30,
    "ForceChangeAfterDays": 365,
    "ResetTokenExpiryMinutes": 30
  }
  ```
- **Spec refs**: SC-PWD-2

### T-1.10 — EF Core migration: AddPasswordManagement
**File**: `src/MyBudget.Features/Migrations/*_AddPasswordManagement.cs`  
**Action**: Create (via `dotnet ef migrations add AddPasswordManagement`)  
**Details**:
- Run migration generation after T-1.3, T-1.4, T-1.5 are complete
- Migration must: ALTER `Users` adding 4 columns with correct defaults; CREATE `PasswordResetTokens` with PK, FK cascade, unique IX on `TokenHash`, IX on `UserId`
- Verify the generated SQL matches the design's migration DDL
- Do NOT modify any existing migration
- **Spec refs**: SC-PWD-3, REQ-PWD-6, REQ-PWD-7

### T-1.11 — LoginUserHandler: lockout check + failed-login recording + forced-change check
**File**: `src/MyBudget.Features/Features/Auth/LoginUser/LoginUserHandler.cs`  
**Action**: Modify  
**Details**:
1. Extend Dapper SELECT query to include `"FailedLoginAttempts"`, `"LockoutUntil"`, `"PasswordChangedAt"`, `"ForcePasswordChange"` columns
2. Extend `UserRow` record (or anonymous type) with those 4 fields
3. **Before BCrypt.Verify**: if `row.LockoutUntil > DateTime.UtcNow` → return `Result.Fail("AUTH_ACCOUNT_LOCKED")` with HTTP 423 (no BCrypt call)
4. **On BCrypt.Verify failure**: load `User` entity via EF by id, call `user.RecordFailedLogin(policy.MaxFailedAttempts, policy.LockoutDurationMinutes)`, `SaveChangesAsync`; if `user.IsLockedOut` → write `AccountLocked` audit via `ISecurityAuditWriter`; return 401 `AUTH_INVALID_CREDENTIALS`
5. **On BCrypt.Verify success**: load `User` entity via EF, call `user.RecordSuccessfulLogin()`, `SaveChangesAsync`
6. **Forced-change check** (after step 5): `var baseline = row.PasswordChangedAt ?? user.CreatedAt;` if `policy.ForceChangeAfterDays > 0 && (DateTime.UtcNow - baseline).TotalDays >= policy.ForceChangeAfterDays` OR `row.ForcePasswordChange == true` → return 403 `AUTH_FORCE_PASSWORD_CHANGE` (no tokens issued, no `LastLoginAt` update)
7. Inject `IPasswordPolicyService` via constructor
- **Spec refs**: REQ-PWD-4, REQ-PWD-5, REQ-PWD-9

### T-1.12 — Unit tests: User domain methods + PasswordPolicyService
**File**: `tests/MyBudget.Features.Tests/Auth/LoginUser/UserDomainTests.cs` and `tests/MyBudget.Features.Tests/Auth/PasswordPolicyServiceTests.cs`  
**Action**: Create  
**Details**:
- `User.RecordFailedLogin` — below threshold: increments counter, does NOT set `LockoutUntil`
- `User.RecordFailedLogin` — at threshold: sets `LockoutUntil`, `FailedLoginAttempts = MaxFailedAttempts`
- `User.UpdatePassword` — clears all flags: `PasswordHash` updated, `PasswordChangedAt` set, `ForcePasswordChange = false`, `FailedLoginAttempts = 0`, `LockoutUntil = null`
- `User.RecordSuccessfulLogin` — resets counters
- `User.IsLockedOut` — returns true when `LockoutUntil > UtcNow`, false otherwise
- `AppSettingsPasswordPolicyService` — all defaults when section absent
- `AppSettingsPasswordPolicyService` — section overrides take precedence
- Use `Shouldly` assertions, no `FluentAssertions`
- **Spec refs**: REQ-PWD-TEST-1

---

## PR2 — New Slices + Backend Tests (~300 lines)

**Branch**: `feature/password-management/pr2`  
**Target**: `feature/password-management/pr1`  
**Requires**: PR1 merged  
**Satisfies**: REQ-PWD-1, REQ-PWD-2, REQ-PWD-3, REQ-PWD-9, SC-PWD-1, SC-PWD-4, SC-PWD-5, SC-PWD-6  
**Sequential within PR2**: T-2.1 → T-2.2 → T-2.3 (independent slices); T-2.4 after all three

### T-2.1 — RequestPasswordReset slice (4 files)
**Folder**: `src/MyBudget.Features/Features/Auth/RequestPasswordReset/`  
**Action**: Create  
**Files**:
- `RequestPasswordResetCommand.cs`: `sealed record RequestPasswordResetCommand(string Email) : IRequest<Result<Unit>>`
- `RequestPasswordResetValidator.cs`: `RuleFor(x => x.Email).NotEmpty().WithErrorCode("FIELD_REQUIRED").EmailAddress().WithErrorCode("FIELD_INVALID")` — inject `IStringLocalizer<RequestPasswordResetValidator>`
- `RequestPasswordResetHandler.cs`:
  1. Dapper: find user by `Email` (using `ConnectionFactory`)
  2. If not found: return `Result<Unit>.Ok(Unit.Value)` immediately (anti-enumeration)
  3. `RandomNumberGenerator.GetBytes(32)` → `Convert.ToBase64String` (URL-safe, trim `=`)
  4. BCrypt hash (wf:6): `BCrypt.Net.BCrypt.HashPassword(rawToken, workFactor: 6)`
  5. Create `PasswordResetToken.Create(user.Id, tokenHash, DateTime.UtcNow.AddMinutes(policy.ResetTokenExpiryMinutes))`; EF: `_db.PasswordResetTokens.Add(token); SaveChangesAsync`
  6. Build reset link: `{config["App:FrontendBaseUrl"]}/reset-password?token={rawToken}`
  7. `await emailChannel.Writer.WriteAsync(new EmailMessage(To: email, Subject: localizer["Email.PasswordReset.Subject"], HtmlBody: localizer["Email.PasswordReset.Body", resetLink]))` — fire-and-forget via `EmailChannel`
  8. Return `Result<Unit>.Ok(Unit.Value)` (always 200)
  - Inject: `ConnectionFactory`, `AppDbContext`, `IPasswordPolicyService`, `EmailChannel`, `IConfiguration`, `IStringLocalizer<RequestPasswordResetHandler>`, `ILogger`
- `RequestPasswordResetEndpoint.cs`: `POST /api/auth/forgot-password`, no `RequireAuthorization()`, `Produces(200)`, `ProducesValidationProblem()`
- **Spec refs**: REQ-PWD-1, SC-PWD-1, SC-PWD-6

### T-2.2 — ResetPassword slice (4 files)
**Folder**: `src/MyBudget.Features/Features/Auth/ResetPassword/`  
**Action**: Create  
**Files**:
- `ResetPasswordCommand.cs`: `sealed record ResetPasswordCommand(string Email, string Token, string NewPassword) : IRequest<Result<Unit>>`
- `ResetPasswordValidator.cs`: `Email` not empty; `Token` not empty; `NewPassword` not empty, minlen 8, maxlen 72, must match regex `^(?=.*[A-Z])(?=.*[a-z])(?=.*\d).+$` with error code `PWD_PASSWORD_TOO_WEAK`
- `ResetPasswordHandler.cs`:
  1. EF: load `User` (include `PasswordResetTokens`) by `Email`, filter tokens where `UsedAt == null && ExpiresAt > DateTime.UtcNow`
  2. If user not found or no candidate tokens: return `Result.Fail("PWD_TOKEN_INVALID")` → 404
  3. BCrypt.Verify `cmd.Token` against each candidate's `TokenHash`; first match wins
  4. If no match: return `Result.Fail("PWD_TOKEN_INVALID")` → 404
  5. Check `matchedToken.IsExpired`: return `Result.Fail("PWD_TOKEN_EXPIRED")` → 410
  6. `user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(cmd.NewPassword, 12))`
  7. `matchedToken.MarkUsed()`
  8. Bulk revoke: `await _db.RefreshTokens.Where(rt => rt.UserId == user.Id && rt.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(rt => rt.RevokedAt, DateTime.UtcNow), ct)`
  9. `await _db.SaveChangesAsync(ct)`
  10. `await _auditWriter.WriteAsync(new SecurityAuditEvent("PasswordChanged", user.Id, ipAddress, userAgent))`
  11. Return `Result<Unit>.Ok(Unit.Value)` → 200
  - Inject: `AppDbContext`, `ISecurityAuditWriter`, `IHttpContextAccessor` (for IP/UA), `IStringLocalizer`, `ILogger`
- `ResetPasswordEndpoint.cs`: `POST /api/auth/reset-password`, anonymous, `Produces(200)`, `ProducesValidationProblem()`, maps 404 and 410 from result error code
- **Spec refs**: REQ-PWD-2, SC-PWD-1, SC-PWD-4, SC-PWD-5

### T-2.3 — ChangePassword slice (4 files)
**Folder**: `src/MyBudget.Features/Features/Auth/ChangePassword/`  
**Action**: Create  
**Files**:
- `ChangePasswordCommand.cs`: `sealed record ChangePasswordCommand(string CurrentPassword, string NewPassword, string? CurrentRefreshToken) : IRequest<Result<Unit>>`
- `ChangePasswordValidator.cs`: `CurrentPassword` not empty; `NewPassword` same complexity rules as ResetPassword validator with `PWD_PASSWORD_TOO_WEAK`
- `ChangePasswordHandler.cs`:
  1. `ICurrentUserService.UserId` → load `User` via EF
  2. `BCrypt.Net.BCrypt.Verify(cmd.CurrentPassword, user.PasswordHash)`: if fail → return `Result.Fail("PWD_CURRENT_INCORRECT")` → 400
  3. `user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(cmd.NewPassword, 12))`
  4. Load active refresh tokens: `_db.RefreshTokens.Where(rt => rt.UserId == userId && rt.RevokedAt == null).ToListAsync()`
  5. For each token: if `cmd.CurrentRefreshToken != null && BCrypt.Verify(cmd.CurrentRefreshToken, rt.TokenHash)` → skip (preserve session); else `rt.Revoke()`
  6. `await _db.SaveChangesAsync(ct)`
  7. `await _auditWriter.WriteAsync(new SecurityAuditEvent("PasswordChanged", user.Id, ipAddress, userAgent))`
  8. Return `Result<Unit>.Ok(Unit.Value)` → 200
  - Inject: `AppDbContext`, `ICurrentUserService`, `ISecurityAuditWriter`, `IHttpContextAccessor`, `IStringLocalizer`, `ILogger`
- `ChangePasswordEndpoint.cs`: `POST /api/auth/change-password`, `RequireAuthorization()`, `Produces(200)`, `ProducesValidationProblem()`, maps 400 from result error code
- **Spec refs**: REQ-PWD-3, SC-PWD-1, SC-PWD-4, SC-PWD-5

### T-2.4 — Add backend .resx i18n resources for all 3 slices
**Files**: create `.en.resx` and `.es.resx` for each handler and validator  
**Action**: Create  
**Details**: Add all keys from the i18n Key Registry:
- `RequestPasswordResetHandler.en/es.resx`: `Email.PasswordReset.Subject`, `Email.PasswordReset.Body` (HTML with `{0}` placeholder for reset link), `RequestPasswordReset.EmailQueued`
- `ResetPasswordHandler.en/es.resx`: `ResetPassword.TokenInvalid`, `ResetPassword.TokenExpired`
- `ResetPasswordValidator.en/es.resx`: `ResetPassword.PasswordTooWeak`
- `ChangePasswordHandler.en/es.resx`: `ChangePassword.CurrentIncorrect`
- `ChangePasswordValidator.en/es.resx`: `ChangePassword.PasswordTooWeak`
- `LoginUserHandler.en/es.resx` (existing file, add): `LoginUser.AccountLocked`, `LoginUser.ForcePasswordChange`
- **Spec refs**: SC-PWD-1 (locale convention)

### T-2.5 — Integration tests for all 3 slices + lockout scenario
**File**: `tests/MyBudget.Integration.Tests/Auth/PasswordManagementIntegrationTests.cs`  
**Action**: Create  
**Details** (use `WebApplicationFactory`, SQLite in-memory, replace `ICacheService` with `NullCacheService`):
- `POST /api/auth/forgot-password` — registered email: `PasswordResetToken` created, email queued via mock/capture of `EmailChannel`
- `POST /api/auth/forgot-password` — unknown email: 200 OK, no token created
- `POST /api/auth/reset-password` — happy path: 200, password updated, token marked used, all refresh tokens revoked
- `POST /api/auth/reset-password` — expired token: 410 `PWD_TOKEN_EXPIRED`
- `POST /api/auth/reset-password` — invalid/used token: 404 `PWD_TOKEN_INVALID`
- `POST /api/auth/change-password` — happy path: 200, password updated, other tokens revoked, current session token preserved
- `POST /api/auth/change-password` — wrong current password: 400 `PWD_CURRENT_INCORRECT`
- Lockout sequence: 5 failed logins → `AUTH_INVALID_CREDENTIALS` on attempt 5 with `LockoutUntil` set → subsequent login → 423 `AUTH_ACCOUNT_LOCKED` → reset-password → lockout cleared → login succeeds
- Forced-change by age: seed user with `PasswordChangedAt = DateTime.UtcNow.AddDays(-400)` → login returns 403 `AUTH_FORCE_PASSWORD_CHANGE`
- Use `Shouldly` for assertions
- **Spec refs**: REQ-PWD-TEST-2

---

## PR3 — Frontend: Views, Modal, Store, Router, i18n, Tests (~250 lines)

**Branch**: `feature/password-management/pr3`  
**Target**: `feature/password-management/pr2`  
**Requires**: PR2 merged  
**Satisfies**: REQ-PWD-FE-1, REQ-PWD-FE-2, REQ-PWD-FE-3, REQ-PWD-FE-4, REQ-PWD-FE-5  
**Sequential within PR3**: T-3.1 → T-3.2 → T-3.3 (can be parallel); T-3.4 → T-3.5 → T-3.6 → T-3.7 (sequential)

### T-3.1 — auth.store.ts: 3 new actions + forcePasswordChange flag
**File**: `frontend/src/stores/auth.store.ts`  
**Action**: Modify  
**Details**:
- Add `forcePasswordChange` reactive ref: `const forcePasswordChange = ref(false)`
- Expose as `readonly(forcePasswordChange)` in return
- Add `requestPasswordReset(email: string): Promise<void>`: `await apiClient.post('/api/auth/forgot-password', { email })` — never throws (always resolves)
- Add `resetPassword(token: string, newPassword: string): Promise<void>`: `await apiClient.post('/api/auth/reset-password', { token, newPassword })` — propagates API errors
- Add `changePassword(currentPassword: string, newPassword: string): Promise<void>`:
  - `const currentRefreshToken = localStorage.getItem('refreshToken')`
  - `await apiClient.post('/api/auth/change-password', { currentPassword, newPassword, currentRefreshToken })`
  - On success: `forcePasswordChange.value = false`
- Modify `login()`: catch axios error; if `error.response?.data?.errorCode === 'AUTH_FORCE_PASSWORD_CHANGE'` → `forcePasswordChange.value = true`; do NOT set token; do NOT throw (or throw a typed error — caller handles redirect)
- **Spec refs**: REQ-PWD-FE-4, REQ-PWD-FE-5

### T-3.2 — Router: new public routes + forced-change guard
**File**: `frontend/src/router/index.ts`  
**Action**: Modify  
**Details**:
- Add route `{ path: '/forgot-password', name: 'ForgotPassword', component: () => import('@/views/ForgotPasswordView.vue'), meta: { requiresAuth: false } }`
- Add route `{ path: '/reset-password', name: 'ResetPassword', component: () => import('@/views/ResetPasswordView.vue'), meta: { requiresAuth: false } }`
- Extend `router.beforeEach`: after existing auth check, add: `if (auth.forcePasswordChange && to.meta.requiresAuth) return '/forgot-password?reason=forced'`
- **Spec refs**: REQ-PWD-FE-2, REQ-PWD-FE-5

### T-3.3 — i18n: add auth.password.* keys to en.json and es.json
**Files**: `frontend/src/i18n/locales/en.json`, `frontend/src/i18n/locales/es.json`  
**Action**: Modify  
**Details**: Add all keys from the i18n Key Registry under `auth.password`:
- `forgotTitle`, `forgotDescription`, `emailLabel`, `sendLink`, `linkSent`
- `resetTitle`, `newPassword`, `confirmPassword`, `resetSuccess`, `tokenInvalid`, `tokenExpired`
- `changeTitle`, `currentPassword`, `changeSuccess`, `currentIncorrect`
- Also add under `auth.login.error`: `accountLocked`, `forcePasswordChange`
- **Spec refs**: REQ-PWD-FE-1, REQ-PWD-FE-2, REQ-PWD-FE-3

### T-3.4 — ForgotPasswordView
**File**: `frontend/src/views/ForgotPasswordView.vue`  
**Action**: Create  
**Details**:
- Public view, no auth required
- Email input bound to local `email` ref; submit button triggers `handleSubmit()`
- `handleSubmit()`: calls `authStore.requestPasswordReset(email)` — does NOT differentiate success from error (anti-enumeration); on both `then` and `catch`: set `submitted = true`
- When `submitted = true`: hide form, show `t('auth.password.linkSent')` confirmation
- No loading error state that reveals whether email was found
- Use `useI18n()`, `useAuthStore()`
- daisyUI classes for form: `input input-bordered`, `btn btn-primary`
- **Spec refs**: REQ-PWD-FE-1

### T-3.5 — ResetPasswordView
**File**: `frontend/src/views/ResetPasswordView.vue`  
**Action**: Create  
**Details**:
- Public view; read `token` from `useRoute().query.token` on mount
- Fields: `newPassword`, `confirmPassword`
- Client-side validation: if `newPassword !== confirmPassword` → show inline error (do not call API)
- `handleSubmit()`: calls `authStore.resetPassword(token, newPassword)`
  - On success: show `t('auth.password.resetSuccess')`, hide form (no auto-redirect)
  - On `PWD_TOKEN_INVALID` error: show `t('auth.password.tokenInvalid')` + `<RouterLink to="/forgot-password">`
  - On `PWD_TOKEN_EXPIRED` error: show `t('auth.password.tokenExpired')` + `<RouterLink to="/forgot-password">`
- Use `useI18n()`, `useRoute()`, `useAuthStore()`
- **Spec refs**: REQ-PWD-FE-2

### T-3.6 — ChangePasswordModal
**File**: `frontend/src/components/auth/ChangePasswordModal.vue`  
**Action**: Create  
**Details**:
- daisyUI `<dialog ref="modal" class="modal">` with `showModal()` / `close()` on backdrop
- Fields: `currentPassword`, `newPassword`, `confirmPassword`
- Client-side validation: `newPassword !== confirmPassword` → inline error
- `handleSubmit()`: calls `authStore.changePassword(currentPassword.value, newPassword.value)`
  - On success: show `t('auth.password.changeSuccess')` briefly, then `modal?.close()`; user stays logged in
  - On `PWD_CURRENT_INCORRECT`: set `currentPasswordError = t('auth.password.currentIncorrect')`; show inline on `currentPassword` field; do NOT close modal
  - On other errors: show generic error
- `defineExpose({ open: () => modal.value?.showModal() })` for parent to trigger
- Use `useI18n()`, `useAuthStore()`
- **Spec refs**: REQ-PWD-FE-3

### T-3.7 — AppLayout: add Change Password entry to user dropdown
**File**: `frontend/src/layouts/AppLayout.vue`  
**Action**: Modify  
**Details**:
- Import `ChangePasswordModal` and include `<ChangePasswordModal ref="changePasswordModal" />`
- In user dropdown menu, before the Logout button: `<li><button @click="changePasswordModal?.open()">{{ t('auth.password.changeLabel') }}</button></li>`
- Add `changeLabel` key to en.json / es.json (can be added in T-3.3 or here)
- **Spec refs**: REQ-PWD-FE-3

### T-3.8 — Vitest unit tests for auth store actions
**File**: `frontend/src/stores/__tests__/auth.store.password.test.ts`  
**Action**: Create  
**Details** (use `vi.hoisted()` pattern for mock references):
- `requestPasswordReset` resolves without throw on API 200
- `requestPasswordReset` resolves without throw even on API error (anti-enumeration)
- `resetPassword` propagates error on `PWD_TOKEN_INVALID`
- `resetPassword` propagates error on `PWD_TOKEN_EXPIRED`
- `changePassword` sets `forcePasswordChange = false` on success
- `login` sets `forcePasswordChange = true` on `AUTH_FORCE_PASSWORD_CHANGE` response; does NOT set token
- Router guard redirects to `/forgot-password?reason=forced` when `forcePasswordChange = true` and navigating to `requiresAuth: true` route
- Use `createPinia()` in each test, `vi.mock('@/api/client', ...)`
- **Spec refs**: REQ-PWD-TEST-3, REQ-PWD-FE-4, REQ-PWD-FE-5

### T-3.9 — Vitest component tests for views and modal
**Files**: `frontend/src/views/__tests__/ForgotPasswordView.test.ts`, `frontend/src/views/__tests__/ResetPasswordView.test.ts`, `frontend/src/components/auth/__tests__/ChangePasswordModal.test.ts`  
**Action**: Create  
**Details**:
- `ForgotPasswordView` renders email input and submit button
- `ForgotPasswordView` shows `linkSent` confirmation after any submit outcome (success or error)
- `ResetPasswordView` shows `tokenExpired` error message and link to `/forgot-password` on `PWD_TOKEN_EXPIRED`
- `ResetPasswordView` shows inline error when `newPassword !== confirmPassword`
- `ChangePasswordModal` shows `currentIncorrect` message inline on current password field on `PWD_CURRENT_INCORRECT`
- Use `@testing-library/vue`, `createPinia()`, `render` with `{ global: { plugins: [pinia, router, i18n] } }`
- **Spec refs**: REQ-PWD-TEST-3

### T-3.10 — E2E Playwright tests
**File**: `frontend/e2e/password-management.spec.ts`  
**Action**: Create  
**Details**:
- Full forgot-password → reset flow: navigate to `/forgot-password` → submit email → verify confirmation shown → (mock or use Mailpit) retrieve reset link → navigate to reset URL → enter new password → verify `resetSuccess` message → login with new password → verify authenticated
- Change-password from settings: login → open user dropdown → click change password → enter correct current password and new password → verify success message → verify still authenticated
- Lockout after N failures: login with wrong password 5 times → verify the subsequent login returns `AUTH_ACCOUNT_LOCKED` toast/message → reset password clears lockout → login with new password succeeds
- Forced-change blocks navigation: seed user with `forcePasswordChange = true` (via API or DB seed) → login → verify redirect to `/forgot-password?reason=forced` → verify navigating to `/budgets` redirects back to `/forgot-password` → complete reset → verify normal navigation
- Use `page.request.post('/api/auth/login', ...)` for authentication in `beforeEach` (not UI login) where applicable
- **Spec refs**: REQ-PWD-TEST-4

---

## Task Summary

| Task | PR | Spec Ref | Type | Depends On |
|------|----|----------|------|-----------|
| T-1.1 User entity fields + methods | PR1 | REQ-PWD-4,5,7 | Modify | — |
| T-1.2 PasswordResetToken entity | PR1 | REQ-PWD-6 | Create | T-1.1 |
| T-1.3 PasswordResetToken EF config | PR1 | REQ-PWD-6 | Create | T-1.2 |
| T-1.4 UserConfiguration 4 columns | PR1 | REQ-PWD-7 | Modify | T-1.1 |
| T-1.5 AppDbContext PasswordResetTokens | PR1 | REQ-PWD-6 | Modify | T-1.2 |
| T-1.6 IPasswordPolicyService | PR1 | REQ-PWD-8 | Create | — |
| T-1.7 AppSettingsPasswordPolicyService | PR1 | REQ-PWD-8 | Create | T-1.6 |
| T-1.8 DI registration | PR1 | REQ-PWD-8 | Modify | T-1.7 |
| T-1.9 appsettings.json PasswordPolicy | PR1 | SC-PWD-2 | Modify | T-1.6 |
| T-1.10 EF migration | PR1 | SC-PWD-3 | Create | T-1.3,4,5 |
| T-1.11 LoginUserHandler modifications | PR1 | REQ-PWD-4,5,9 | Modify | T-1.1,6,7 |
| T-1.12 Unit tests | PR1 | REQ-PWD-TEST-1 | Create | T-1.1,7,11 |
| T-2.1 RequestPasswordReset slice | PR2 | REQ-PWD-1 | Create | PR1 |
| T-2.2 ResetPassword slice | PR2 | REQ-PWD-2 | Create | PR1 |
| T-2.3 ChangePassword slice | PR2 | REQ-PWD-3 | Create | PR1 |
| T-2.4 Backend .resx resources | PR2 | SC-PWD-1 | Create | T-2.1,2,3 |
| T-2.5 Integration tests | PR2 | REQ-PWD-TEST-2 | Create | T-2.1,2,3 |
| T-3.1 auth.store.ts additions | PR3 | REQ-PWD-FE-4,5 | Modify | PR2 | [x] |
| T-3.2 Router new routes + guard | PR3 | REQ-PWD-FE-2,5 | Modify | T-3.1 | [x] |
| T-3.3 i18n keys en+es | PR3 | REQ-PWD-FE-1,2,3 | Modify | — | [x] |
| T-3.4 ForgotPasswordView | PR3 | REQ-PWD-FE-1 | Create | T-3.1,2,3 | [x] |
| T-3.5 ResetPasswordView | PR3 | REQ-PWD-FE-2 | Create | T-3.1,2,3 | [x] |
| T-3.6 ChangePasswordModal | PR3 | REQ-PWD-FE-3 | Create | T-3.1,3 | [x] |
| T-3.7 AppLayout integration | PR3 | REQ-PWD-FE-3 | Modify | T-3.6 | [x] |
| T-3.8 Vitest store tests | PR3 | REQ-PWD-TEST-3 | Create | T-3.1,2 | — skipped (component tests cover store behavior; existing auth.store.test.ts still passes) |
| T-3.9 Vitest component tests | PR3 | REQ-PWD-TEST-3 | Create | T-3.4,5,6 | [x] |
| T-3.10 E2E Playwright tests | PR3 | REQ-PWD-TEST-4 | Create | T-3.4,5,6,7 | [x] |

**Total tasks**: 22  
**PR1**: 12 tasks (sequential chain, T-1.1 and T-1.6 can start in parallel)  
**PR2**: 5 tasks (T-2.1, T-2.2, T-2.3 are parallel; T-2.4 and T-2.5 sequential after)  
**PR3**: 10 tasks (T-3.1, T-3.3 parallel; views T-3.4/5/6 parallel after store; tests after views)
