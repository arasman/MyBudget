# Proposal: Password Management

## Intent

Users have no way to recover a forgotten password, change their password from account settings, or be forced to rotate credentials on a policy-driven interval. Compromised or weak passwords cannot be mitigated without direct DB intervention. This change adds the full password lifecycle: recovery by email, authenticated change, forced-change policy, and account lockout after repeated failures.

## Scope

### In Scope

- `POST /api/auth/forgot-password` — anonymous; always 200 (anti-enumeration); sends reset email when user exists
- `POST /api/auth/reset-password` — anonymous; validates token + sets new password; clears lockout + force-change flag; writes `PasswordChanged` audit event
- `POST /api/auth/change-password` — authenticated; verifies current password; writes `PasswordChanged` audit event
- Login lockout: `PasswordPolicy:MaxFailedAttempts` + `PasswordPolicy:LockoutDurationMinutes`; error code `AUTH_ACCOUNT_LOCKED`; reset-password clears lockout
- Forced-change: `PasswordPolicy:ForceChangeAfterDays`; detected at login; error code `AUTH_FORCE_PASSWORD_CHANGE` (no token issued); change-password clears flag
- `PasswordChanged` + `AccountLocked` SecurityAuditLog events via `ISecurityAuditWriter`
- `PasswordResetToken` entity (mirrors Invitation: BCrypt-hashed token, ExpiresAt, UsedAt)
- `IPasswordPolicyService` backed by appsettings `PasswordPolicy` section
- 4 new User columns: `FailedLoginAttempts`, `LockoutUntil`, `PasswordChangedAt`, `ForcePasswordChange`
- Frontend: ForgotPasswordView, ResetPasswordView, ChangePasswordModal, AppLayout dropdown entry, i18n keys

### Out of Scope

- OAuth, SSO, MFA
- Admin-forced reset, password history, admin unlock dashboard

## Capabilities

### New Capabilities

- `password-recovery`: forgot-password request + token-based reset-password flow
- `password-change`: authenticated change-password from account settings
- `password-policy`: forced-change interval + account lockout configuration and enforcement

### Modified Capabilities

- `auth`: LoginUser handler gains lockout check (before BCrypt.Verify) and forced-change detection; two new error codes added to login error table

## Approach

Standalone `PasswordResetToken` table mirroring `Invitation` pattern (BCrypt hash, workFactor 6, 64-byte random token). `IPasswordPolicyService` reads `PasswordPolicy` appsettings section (same pattern as `IAuditRetentionPolicy`). Three new VSA slices (RequestPasswordReset, ResetPassword, ChangePassword). LoginUserHandler modified for lockout + forced-change enforcement. One EF migration (additive columns on Users + new PasswordResetTokens table).

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SharedKernel/Entities/User.cs` | Modified | 4 new fields + domain methods |
| `SharedKernel/Entities/PasswordResetToken.cs` | New | Token entity mirroring Invitation |
| `SharedKernel/Services/IPasswordPolicyService.cs` | New | Policy interface + appsettings impl |
| `Features/Auth/LoginUser/LoginUserHandler.cs` | Modified | Lockout + forced-change enforcement |
| `Features/Auth/RequestPasswordReset/` | New | 4-file VSA slice |
| `Features/Auth/ResetPassword/` | New | 4-file VSA slice |
| `Features/Auth/ChangePassword/` | New | 4-file VSA slice |
| `frontend/src/views/ForgotPasswordView.vue` | New | Public view |
| `frontend/src/views/ResetPasswordView.vue` | New | Public view |
| `frontend/src/components/auth/ChangePasswordModal.vue` | New | Modal from AppLayout |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Lockout check after BCrypt.Verify leaks timing info | Med | Enforce lockout check BEFORE BCrypt.Verify in LoginUser |
| User enumeration via forgot-password response time | Low | Always return 200; send email conditionally |
| ForcePasswordChange left stale after reset | Low | Clear in same SaveChangesAsync as new hash |

## Rollback Plan

Drop `PasswordResetTokens` table + remove 4 added User columns via reverse migration. Revert LoginUserHandler to pre-lockout logic. Remove 3 new VSA slice folders. Frontend: remove new views/components and revert router/store/i18n changes.

## Dependencies

- `ISecurityAuditWriter` (audit-log change — already shipped)
- `IEmailSender` / `EmailChannel` (already operational)
- `Invitation` BCrypt-token pattern (already in codebase)

## Success Criteria

- [ ] Forgot-password email arrives in Mailpit with valid reset link
- [ ] Reset-password with valid token sets new password and clears lockout/force-change
- [ ] Change-password from settings works with correct current password
- [ ] Account locks after N failed attempts; locked account returns `AUTH_ACCOUNT_LOCKED`
- [ ] Forced-change detected at login returns `AUTH_FORCE_PASSWORD_CHANGE`; no token issued
- [ ] `PasswordChanged` and `AccountLocked` events appear in SecurityAuditLog
- [ ] All 4 test layers pass: unit, integration, Vitest, E2E

## Delivery Shape

### Backend — 2 PRs

- **PR1** (~300 lines): User entity changes, PasswordResetToken entity, PasswordPolicyService, EF migration, LoginUser lockout+forced-change enforcement, AccountLocked audit event
- **PR2** (~300 lines): RequestPasswordReset + ResetPassword + ChangePassword slices, PasswordChanged audit event, backend tests

### Frontend — 1 PR

- **PR3** (~250 lines): ForgotPasswordView, ResetPasswordView, ChangePasswordModal, auth store actions, router, i18n keys, Vitest + E2E tests

## Open Questions for Spec Phase

1. Should `ResetTokenExpiryMinutes` default to 60 or 30?
2. Should a successful password reset invalidate all existing refresh tokens for that user (forced re-login on all devices)?
3. Should the forced-change check use `PasswordChangedAt ?? CreatedAt` as baseline, or should existing users be exempt until their next password change?
