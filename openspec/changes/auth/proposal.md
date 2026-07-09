# Proposal: Auth Feature (MVP A)

## Intent

MyBudget has no authentication. Every route is publicly accessible and there are no users, roles, or budget membership concepts.
This change introduces the full auth foundation: registration, login, JWT + refresh token, per-budget role authorization, and a budget invitation flow — all required before any real multi-user feature can ship.

## Scope

### In Scope
- NuGet packages: `BCrypt.Net-Next`, `Microsoft.AspNetCore.Authentication.JwtBearer`
- JWT configuration via User Secrets (dev) and environment variables (prod)
- DB entities: `User`, `RefreshToken`, `Budget`, `BudgetMembership`, `Invitation`
- EF Core migration `AddAuthTables` (separate from `InitialCreate` — do NOT modify that migration)
- 7 backend VSA slices (see Backend Slices below)
- Per-budget authorization via custom `IAuthorizationRequirement` / `IAuthorizationHandler` (reads `BudgetMembership` from DB)
- Frontend: wire `auth.store.ts`, `LoginView.vue`, create `RegisterView.vue`, `AcceptInvitationView.vue`, `InviteUserModal.vue`
- i18n: add register + invitation keys to `en.json` / `es.json`
- Email flows: invitation email via existing MailKit + Channel pipeline

### Out of Scope
- Password reset / forgot-password flow
- OAuth / social login
- Global app-level roles (all roles are budget-scoped)
- Two-factor authentication
- Budget CRUD beyond `CreateBudget` (deferred to Budget feature)
- Import / export
- Audit log

## Capabilities

### New Capabilities
- `user-registration`: Email + password registration with BCrypt hashing; preferredLocale stored
- `user-login`: JWT access token (15–60 min) + rotating refresh token (7 days, hashed in DB, single-use)
- `token-refresh`: Silent re-auth via refresh token rotation; revoked on reuse (theft detection)
- `user-logout`: Revokes current refresh token server-side
- `current-user`: Authenticated endpoint returning user profile (`GET /api/auth/me`)
- `budget-invitation`: Owner/admin sends email invite to a budget + role; invitee accepts via token link
- `per-budget-authorization`: Custom `IAuthorizationHandler` resolves role from `BudgetMembership` at request time — NO budget roles baked into JWT

### Modified Capabilities
- None

## Approach

**JWT + rotating refresh token stored hashed in DB.**

Access token carries `sub`, `email`, `jti`, `iat`, `exp` only — no budget roles (avoids stale-role bugs). Per-budget authorization is handled by a custom `IAuthorizationRequirement` / `IAuthorizationHandler` that reads `BudgetMembership` from the DB on each authorized request. This trades a DB read per request for correctness — acceptable at TFM scale.

Passwords use BCrypt workFactor 12. Refresh tokens are single-use, stored as BCrypt hash; on rotation the old token is revoked and replaced. Reuse of a revoked token revokes the entire token family.

Frontend stores token in `localStorage` — DOMPurify is already wired, acceptable for TFM scope.

Email flows (invitation) use the existing MailKit + Channel pipeline: handler writes to `EmailChannel`, `EmailBackgroundService` sends via SMTP → Mailpit (dev).

## DB Entities

| Entity | Key Fields |
|--------|-----------|
| `User` | Id, Email (unique), PasswordHash, FirstName, LastName, PreferredLocale, LastLoginAt |
| `RefreshToken` | Id, UserId (FK), TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenId |
| `Budget` | Id, Name, OwnerId (FK), CreatedAt |
| `BudgetMembership` | Id, BudgetId (FK), UserId (FK), Role (enum: owner/admin/operator/read-only), JoinedAt |
| `Invitation` | Id, BudgetId (FK), InviteeEmail, Role, TokenHash, ExpiresAt, UsedAt, InvitedByUserId (FK) |

Budget creator auto-becomes `owner` via `BudgetMembership` row created in the same transaction as the budget.

## Backend Slices

| # | Slice | Method + Route |
|---|-------|---------------|
| 1 | `RegisterUser` | POST `/api/auth/register` |
| 2 | `LoginUser` | POST `/api/auth/login` |
| 3 | `RefreshToken` | POST `/api/auth/refresh` |
| 4 | `LogoutUser` | POST `/api/auth/logout` |
| 5 | `GetCurrentUser` | GET `/api/auth/me` (Dapper) |
| 6 | `InviteUserToBudget` | POST `/api/budgets/{id}/invitations` |
| 7 | `AcceptInvitation` | POST `/api/auth/invitations/accept` |

Each slice: 4 files — `Command/Validator/Handler/Endpoint`. Slices never reference each other. Handlers return `ValueTask<Result<T>>`.

## Frontend Changes

| File | Action |
|------|--------|
| `src/stores/auth.store.ts` | Add `User` type; implement `login`, `register`, `logout`, `refresh` actions; add 401 interceptor |
| `src/views/auth/LoginView.vue` | Wire submit logic (stub exists) |
| `src/views/auth/RegisterView.vue` | Create new — route `/register` |
| `src/views/auth/AcceptInvitationView.vue` | Create new — route `/invitations/accept` |
| `src/components/budget/InviteUserModal.vue` | Create new — triggered from budget members section |
| `src/i18n/locales/en.json` | Add register + invitation keys |
| `src/i18n/locales/es.json` | Add register + invitation keys |

## Email Flows

- **Invitation email**: `InviteUserToBudget` handler writes `InvitationEmail` message to `EmailChannel` → `EmailBackgroundService` sends via MailKit → Mailpit (dev) / SMTP (prod). Email contains signed invitation token link.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `MyBudget.Features/Auth/` | New | 7 VSA slices |
| `MyBudget.Features/Budgets/` | New | `CreateBudget` slice (required by invitation flow) |
| `MyBudget.Features/SharedKernel/` | Modified | Add `User`, `RefreshToken`, `Budget`, `BudgetMembership`, `Invitation` entities + EF config |
| `MyBudget.Features/SharedKernel/Authorization/` | New | `BudgetRoleRequirement` + `BudgetRoleHandler` |
| `MyBudget.Api/Program.cs` | Modified | Configure JWT scheme, register authorization policies |
| `src/stores/auth.store.ts` | Modified | Wire actions and 401 interceptor |
| `src/views/auth/` | Modified/New | Wire `LoginView`, add `RegisterView`, `AcceptInvitationView` |
| `src/components/budget/` | New | `InviteUserModal.vue` |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| NuGet packages missing — nothing compiles without them | High | Add `BCrypt.Net-Next` + `JwtBearer` as first task in sdd-tasks |
| JWT key leaks into `appsettings.json` | Med | Enforce User Secrets (dev) + env var (prod) in spec; fail startup if key missing |
| `IAuthorizationHandler` causes N+1 on every authorized request | Med | Spec must define caching strategy (memory cache keyed by `userId+budgetId`, short TTL) |
| Refresh token reuse window (concurrent requests) | Low | Accept for TFM scope; document known limitation |
| `InitialCreate` migration accidentally modified | Low | Spec must explicitly state: do NOT touch `InitialCreate`; create `AddAuthTables` only |

## Rollback Plan

1. Drop migration `AddAuthTables` via `dotnet ef migrations remove` (or `database update` to previous migration).
2. Remove NuGet packages `BCrypt.Net-Next` and `Microsoft.AspNetCore.Authentication.JwtBearer`.
3. Revert `Program.cs` JWT/Authorization registration.
4. Delete `MyBudget.Features/Auth/` and `MyBudget.Features/Budgets/` slice folders.
5. Revert frontend files to scaffold stubs via `git checkout`.

All changes are additive — no existing behavior is modified beyond `Program.cs` registration and the frontend stub files.

## Dependencies

- `BCrypt.Net-Next` NuGet package (not yet in any `.csproj`)
- `Microsoft.AspNetCore.Authentication.JwtBearer` NuGet package (not yet in any `.csproj`)
- JWT signing key configured in User Secrets before any test can run
- Existing email pipeline (MailKit + Channel) — already wired, no changes needed

## Success Criteria

- [ ] `POST /api/auth/register` creates a user with BCrypt-hashed password; returns 201
- [ ] `POST /api/auth/login` returns a valid JWT + refresh token; refresh token stored hashed in DB
- [ ] `POST /api/auth/refresh` rotates refresh token; revoked token reuse returns 401
- [ ] `POST /api/auth/logout` revokes refresh token server-side
- [ ] `GET /api/auth/me` returns current user profile when Bearer token is valid
- [ ] `POST /api/budgets/{id}/invitations` sends invitation email via Mailpit (visible in dev)
- [ ] `POST /api/auth/invitations/accept` grants `BudgetMembership`; token is single-use
- [ ] Per-budget authorization blocks requests with insufficient role
- [ ] JWT key is NOT present in any `appsettings.json` file
- [ ] `InitialCreate` migration is unchanged; `AddAuthTables` migration applies cleanly
- [ ] Frontend: login, register, and invitation accept flows work end-to-end
