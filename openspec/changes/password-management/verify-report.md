## Verify Report — password-management

**Verdict**: PASS WITH WARNINGS (W-001 accepted, W-002 through W-004 resolved)
**Date**: 2026-07-13
**Tasks**: 21/22 complete (T-3.8 intentionally skipped; store behavior covered by component tests)
**Tests**: unit 246 | integration 120 | Vitest 110 | E2E 21 (prior cycle) / password-management E2E created, not executed

---

### Pre-Documented Deviations (not flagged as issues)

1. **PWD_TOKEN_EXPIRED returns 404 not 410** — expired tokens pre-filtered by `ExpiresAt > UtcNow` before BCrypt matching; observable behavior is 404 (`PWD_TOKEN_INVALID`). More secure (no timing oracle). Integration test explicitly documents this.
2. **Error code detection via `detail.includes(...)`** — consistent with existing codebase pattern (`RegisterView` uses same approach).
3. **PasswordHistory added beyond original spec scope** — `PasswordHistoryCount=5` policy, `PasswordHistories` table, `PWD_PREVIOUSLY_USED` + `PWD_SAME_AS_CURRENT` error codes. User-confirmed in-scope during apply.
4. **Forgot-password link added to LoginView** — gap discovered during apply, filled.
5. **T-3.8 store unit tests skipped** — 14 component tests cover store actions; existing `auth.store.test.ts` still passes.
6. **i18n key naming (W-001 resolved)** — keys use `Label` suffix (`newPasswordLabel`, `confirmPasswordLabel`, `currentPasswordLabel`) vs spec's bare names. Accepted: the `Label` suffix is consistent with daisyUI's `label-text` pattern used throughout the frontend. No functional impact.

---

### Spec Compliance Matrix

| REQ | Status | Evidence | Notes |
|---|---|---|---|
| REQ-PWD-1 | PASS | `RequestPasswordResetHandler` always returns 200; creates token for known email; no-op for unknown; email queued via `IEmailSender.SendAsync`; 2 integration tests pass | 64 random bytes (SC-PWD-6 compliant) |
| REQ-PWD-2 | PASS* | `ResetPasswordHandler` validates token, updates password (`UpdatePassword` + `ClearLockout`), marks token used, revokes all refresh tokens via `ExecuteUpdateAsync`, writes `PasswordChanged` audit; integration tests cover happy path, expired, invalid, used-token | *Expired returns 404 not 410 — pre-documented deviation; 410 mapping exists in endpoint but handler never emits `PWD_TOKEN_EXPIRED` |
| REQ-PWD-3 | PASS | `ChangePasswordHandler` verifies current password, updates password, revokes other refresh tokens (preserves current via BCrypt matching), writes `PasswordChanged` audit; 3 integration tests pass | |
| REQ-PWD-4 | PASS | `LoginUserHandler` checks `LockoutUntil` BEFORE BCrypt; on failure calls `RecordFailedLogin()`; `wasLocked` triggers `AccountLocked` audit; lockout sequence integration test passes | |
| REQ-PWD-5 | PASS | After BCrypt success: `user.ForcePasswordChange || ageExceeded`; returns 403 `AUTH_FORCE_PASSWORD_CHANGE`; no tokens issued; `LoginUserEndpoint` maps to 403; both flag-path and age-path integration tests pass | W-004 resolved — age path now has dedicated integration test |
| REQ-PWD-6 | PASS | `PasswordResetToken` entity, EF config, migration `AddPasswordManagement` — all present and correct | |
| REQ-PWD-7 | PASS | 4 new User columns in entity + `UserConfiguration`; migration is additive only | |
| REQ-PWD-8 | PASS | `IPasswordPolicyService` in `SharedKernel/Services/`; `AppSettingsPasswordPolicyService` reads `IConfiguration`; 5 properties (4 required + `PasswordHistoryCount`); DI registered; 9 unit tests pass | |
| REQ-PWD-9 | PASS | `PasswordChanged` in ResetPassword + ChangePassword handlers; `AccountLocked` in LoginUser when `wasLocked = true`; both use `ISecurityAuditWriter` | |
| REQ-PWD-FE-1 | PASS | `ForgotPasswordView` at `/forgot-password`; always shows `linkSent` confirmation; force-change banner on `?reason=force`; 4 Vitest tests pass | |
| REQ-PWD-FE-2 | PASS | `ResetPasswordView` at `/reset-password`; reads `token` from query; handles token errors with link to `/forgot-password`; shows `resetSuccess` on success; 6 Vitest tests pass | |
| REQ-PWD-FE-3 | PASS | `ChangePasswordModal` in AppLayout dropdown; `PWD_CURRENT_INCORRECT` → inline error; success → notification + close; user stays logged in; 4 Vitest tests pass | |
| REQ-PWD-FE-4 | PASS | `auth.store.ts` exposes `requestPasswordReset`, `resetPassword`, `changePassword` with correct signatures | |
| REQ-PWD-FE-5 | PASS | `forcePasswordChange` ref; set on `AUTH_FORCE_PASSWORD_CHANGE`; router guard blocks `requiresAuth` routes; redirects to `/forgot-password?reason=force`; cleared on `changePassword()` success | |

### SC Compliance

| SC | Status | Evidence |
|---|---|---|
| SC-PWD-1 | PASS | All 3 endpoints under `/api/auth/` |
| SC-PWD-2 | PASS | `PasswordPolicy` section in appsettings.json; `AppSettingsPasswordPolicyService` reads it with defaults |
| SC-PWD-3 | PASS | Single `AddPasswordManagement` migration; separate `AddPasswordHistory` for PasswordHistory (bonus feature) |
| SC-PWD-4 | PASS | Both ResetPassword and ChangePassword validators: min 8, max 72, uppercase + lowercase + digit regex |
| SC-PWD-5 | PASS | `ISecurityAuditWriter.WriteAsync` called for `PasswordChanged` and `AccountLocked` |
| SC-PWD-6 | PASS | `RandomNumberGenerator.GetBytes(64)`, BCrypt wf:6 hash stored |

---

### Warnings

**[W-001] i18n key naming divergence from spec**
Spec defines `auth.password.newPassword`, `auth.password.confirmPassword`, `auth.password.currentPassword` as flat keys. Implementation uses `auth.password.newPasswordLabel`, `auth.password.confirmPasswordLabel`, `auth.password.currentPasswordLabel`. Functional impact is zero (keys exist and render correctly). Risk: future translation tooling expecting spec-matching keys may miss these.

**[W-002] ~~`User.UpdatePassword()` does not clear lockout fields~~ — RESOLVED**
Added `UpdatePassword_DoesNotClearLockoutFields` unit test (commit 29d362f) which explicitly asserts that `FailedLoginAttempts` and `LockoutUntil` are NOT cleared by `UpdatePassword` — documenting the two-call contract (`UpdatePassword` + `ClearLockout`) in handlers. 246 unit tests pass.

**[W-003] ~~ResetPassword token scan is global~~ — RESOLVED**
`ResetPasswordCommand` now includes `Email`. Handler looks up user by email first, then scans only that user's tokens. Anti-enumeration preserved: unknown email returns `PWD_TOKEN_INVALID`. Reset link now appends `&email=` param. All integration tests updated. 120 integration tests pass (commit 6926b19).

**[W-004] ~~REQ-PWD-5 age-based forced-change path has no dedicated integration test~~ — RESOLVED**
Added `Login_PasswordChangedAtTooOld_Returns403` integration test — seeds `PasswordChangedAt = 400 days ago` via `ExecuteSqlRawAsync`, then asserts `403 AUTH_FORCE_PASSWORD_CHANGE` on login (commit 6926b19).

**[W-005] E2E tests not executed**
`frontend/e2e/auth/password-management.spec.ts` was created with 3 Playwright scenarios but not run (requires a live server). Full forgot-password → reset flow and lockout recovery are not runtime-verified at the E2E layer.

---

### Critical Issues

None.

---

### Task Completion

| PR | Tasks | Status |
|---|---|---|
| PR1 | T-1.1 through T-1.12 | 12/12 complete |
| PR2 | T-2.1 through T-2.5 | 5/5 complete |
| PR3 | T-3.1 through T-3.10 (T-3.8 skipped) | 9/10 — T-3.8 intentional skip, documented |

**Total**: 21/22 — T-3.8 skip is pre-documented and accepted per apply-progress.

---

### Build / Test Evidence

| Suite | Count | Result |
|---|---|---|
| Backend unit | 246 | PASS (+1 W-002 test) |
| Backend integration | 120 | PASS (+1 W-004 test, 14 PasswordManagementTests) |
| Vitest (frontend) | 110 | PASS (16 test files) |
| E2E Playwright | 21 (prior cycle) | password-management E2E created, not run |
