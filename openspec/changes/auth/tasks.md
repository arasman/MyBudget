# Auth Feature — Implementation Tasks

**Change**: auth
**Generated**: 2026-07-08
**Total tasks**: 62 (14 Infrastructure · 20 Backend Slices · 7 Frontend · 6 Backend Unit Tests · 7 Backend Integration Tests · 5 Frontend Tests · 3 E2E Tests)
**Parallelism**: see notes per group

---

## Infrastructure

Tasks 1.1–1.4 can run in parallel (independent files, no shared state).
Tasks 1.5–1.9 depend on 1.1 (AppDbContext must know entity types before configuration files are added).
Task 1.10 depends on 1.5–1.9 (all EF configs must exist before running migrations).
Tasks 1.11–1.14 depend on 1.3 (JwtOptions must exist before JwtTokenService, Program.cs wiring, and policies).

---

### [x] 1.1 Add BCrypt.Net-Next to Features.csproj
**Files**: `Project/src/MyBudget.Features/MyBudget.Features.csproj`
**What**: Add `<PackageReference Include="BCrypt.Net-Next" Version="4.*" />` to the Features project so handlers can call `BCrypt.HashPassword` and `BCrypt.Verify`.
**Verify**: `MyBudget.Features.csproj` contains the BCrypt.Net-Next package reference; `dotnet restore` succeeds without error.

---

### [x] 1.2 Add JwtBearer to Api.csproj
**Files**: `Project/src/MyBudget.Api/MyBudget.Api.csproj`
**What**: Add `<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.*" />` to the Api project so Program.cs can call `AddJwtBearer`.
**Verify**: `MyBudget.Api.csproj` contains the JwtBearer package reference; `dotnet restore` succeeds without error.

---

### [x] 1.3 Add JWT non-secret config to appsettings.json and create JwtOptions record
**Files**:
- `Project/src/MyBudget.Api/appsettings.json`
- `Project/src/MyBudget.Features/SharedKernel/Auth/JwtOptions.cs`
**What**: Add `"JWT": { "Issuer": "MyBudget", "Audience": "MyBudget.Client", "AccessTokenExpiryMinutes": 15 }` to appsettings.json and create the `JwtOptions` sealed record with `Key`, `Issuer`, `Audience`, `AccessTokenExpiryMinutes` properties; also add `"App": { "FrontendBaseUrl": "http://localhost:5173" }` for invitation email links.
**Verify**: `appsettings.json` has the `JWT` section (no `Key` field); `JwtOptions.cs` compiles with all four properties.

---

### [x] 1.4 Add JWT__Key to User Secrets
**Files**: User Secrets store for project `11705838-96dc-4d93-a88f-361bc5876823` (no committed file)
**What**: Run `dotnet user-secrets set "JWT__Key" "<minimum-32-char-random-string>"` in the `MyBudget.Api` directory so the startup guard passes in development.
**Verify**: `dotnet user-secrets list` shows `JWT__Key` set; the value does NOT appear in any committed file.

---

### [x] 1.5 Create all 5 domain entities and BudgetRole enum
**Files**:
- `Project/src/MyBudget.Features/SharedKernel/Entities/BudgetRole.cs`
- `Project/src/MyBudget.Features/SharedKernel/Entities/User.cs`
- `Project/src/MyBudget.Features/SharedKernel/Entities/RefreshToken.cs`
- `Project/src/MyBudget.Features/SharedKernel/Entities/Budget.cs`
- `Project/src/MyBudget.Features/SharedKernel/Entities/BudgetMembership.cs`
- `Project/src/MyBudget.Features/SharedKernel/Entities/Invitation.cs`
**What**: Create `BudgetRole` enum (`Owner=40, Admin=30, Operator=20, ReadOnly=10`) and all five entity classes with the exact fields listed in the design; navigation properties required by EF configurations must be present.
**Verify**: All six files compile without errors; `BudgetRole` values are integer-comparable.

---

### [x] 1.6 Create all 5 EF Core configurations
**Files**:
- `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/UserConfiguration.cs`
- `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/RefreshTokenConfiguration.cs`
- `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/BudgetConfiguration.cs`
- `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/BudgetMembershipConfiguration.cs`
- `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/InvitationConfiguration.cs`
**What**: Implement `IEntityTypeConfiguration<T>` for each entity with all indexes, FK constraints, cascade rules, unique constraints, and `HasConversion<int>()` on `BudgetRole` fields as specified in the design; `AppDbContext.OnModelCreating` already calls `ApplyConfigurationsFromAssembly`, so no manual registration is needed.
**Verify**: All five configuration files compile; EF Core can scaffold the model without validation warnings.

---

### [x] 1.7 Register DbSet properties in AppDbContext
**Files**: `Project/src/MyBudget.Features/SharedKernel/Persistence/AppDbContext.cs`
**What**: Add five `DbSet<T>` properties (`Users`, `RefreshTokens`, `Budgets`, `BudgetMemberships`, `Invitations`) so EF Core migrations and handlers can reference them.
**Verify**: `AppDbContext` exposes all five DbSet properties; `dotnet build` succeeds.

---

### [x] 1.8 Create AddAuthTables EF Core migration
**Files**:
- `Project/src/MyBudget.Features/Migrations/AddAuthTables.cs` (generated)
- `Project/src/MyBudget.Features/Migrations/AddAuthTables.Designer.cs` (generated)
- `Project/src/MyBudget.Features/Migrations/AppDbContextModelSnapshot.cs` (updated)
**What**: Run `dotnet ef migrations add AddAuthTables --project MyBudget.Features --startup-project MyBudget.Api` to generate the migration that creates the five auth tables with the indexes defined in the design; verify `InitialCreate` is NOT modified.
**Verify**: Migration file creates all five tables in FK-dependency order; `InitialCreate` is unchanged; `dotnet ef database update` applies successfully against the local Postgres instance.

---

### [x] 1.9 Create shared auth response DTOs
**Files**:
- `Project/src/MyBudget.Features/SharedKernel/Auth/LoginResponse.cs`
- `Project/src/MyBudget.Features/SharedKernel/Auth/CurrentUserResponse.cs`
- `Project/src/MyBudget.Features/SharedKernel/Auth/BudgetMembershipDto.cs`
**What**: Create the three shared response record types used by multiple slices: `LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn)`, `CurrentUserResponse(...)`, and `BudgetMembershipDto(Guid BudgetId, string BudgetName, string Role)`.
**Verify**: All three files compile; record properties match the spec response contracts.

---

### [x] 1.10 Create JwtTokenService
**Files**: `Project/src/MyBudget.Features/SharedKernel/Auth/JwtTokenService.cs`
**What**: Implement `JwtTokenService` with `GenerateAccessToken(User user) → string` (creates JWT with `sub`, `email`, `jti`, `iat`, `exp` claims; signs with `HmacSha256` using `JwtOptions.Key`) and `GenerateRefreshToken() → string` (`RandomNumberGenerator.GetBytes(64)` → Base64Url); no roles or budget IDs in the token payload.
**Verify**: `JwtTokenService` compiles; `GenerateAccessToken` produces a valid JWT with exactly the five required claims; `GenerateRefreshToken` returns a 64-byte Base64Url string.

---

### [x] 1.11 Register JwtOptions, JwtTokenService, IMemoryCache, and BudgetAuthorizationHandler in ServiceCollectionExtensions
**Files**: `Project/src/MyBudget.Features/Extensions/ServiceCollectionExtensions.cs`
**What**: Add to `AddFeatures`: `services.Configure<JwtOptions>(configuration.GetSection("JWT"))`, `services.AddScoped<JwtTokenService>()`, `services.AddMemoryCache()`, and `services.AddScoped<IAuthorizationHandler, BudgetAuthorizationHandler>()`.
**Verify**: `ServiceCollectionExtensions.cs` compiles with the new registrations; no duplicate service registrations.

---

### [x] 1.12 Create per-budget authorization components
**Files**:
- `Project/src/MyBudget.Features/SharedKernel/Auth/Authorization/BudgetRequirement.cs`
- `Project/src/MyBudget.Features/SharedKernel/Auth/Authorization/BudgetAuthorizationHandler.cs`
- `Project/src/MyBudget.Features/SharedKernel/Auth/Authorization/AuthorizationPolicyExtensions.cs`
**What**: Create `BudgetRequirement(BudgetRole MinimumRole) : IAuthorizationRequirement`; implement `BudgetAuthorizationHandler` that extracts `userId` from `ClaimTypes.NameIdentifier`, `budgetId` from route values, checks `IMemoryCache` (key `"budget-membership:{userId}:{budgetId}"`, TTL 5 min), falls back to Dapper SELECT on miss, and calls `context.Succeed()` if `(int)role >= (int)requirement.MinimumRole`; create `AuthorizationPolicyExtensions.AddBudgetPolicies` that registers the three named policies (`"budget:admin"` → Admin, `"budget:operator"` → Operator, `"budget:read"` → ReadOnly).
**Verify**: All three files compile; `BudgetAuthorizationHandler` correctly calls `Succeed` or `Fail` based on role comparison.

---

### [x] 1.13 Wire JWT Bearer authentication and authorization policies in Program.cs
**Files**: `Project/src/MyBudget.Api/Program.cs`
**What**: Replace the stub `AddAuthentication()` / `AddAuthorization()` with `AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(opts => { /* bind from JwtOptions */ })` and `AddAuthorization(opts => opts.AddBudgetPolicies())`; add the startup guard immediately after: read `JwtOptions` from `builder.Configuration`, throw `InvalidOperationException("JWT:Key is not configured. Set via User Secrets or JWT__Key env var.")` if `Key` is null/empty.
**Verify**: Application starts successfully when `JWT__Key` is set; throws `InvalidOperationException` at startup if `JWT__Key` is absent; JWT middleware validates tokens correctly.

---

### [x] 1.14 Add App__FrontendBaseUrl to appsettings.json
**Files**: `Project/src/MyBudget.Api/appsettings.json`
**What**: Add `"App": { "FrontendBaseUrl": "http://localhost:5173" }` to appsettings.json so `InviteUserToBudgetHandler` can construct invitation email links.
**Verify**: The `App` section appears in `appsettings.json`; value does NOT contain secrets.

---

## Backend Slices

Slices are ordered by dependency: RegisterUser and LoginUser require JwtTokenService (1.10) and entities (1.5–1.8). RefreshToken depends on LoginUser's token format. LogoutUser and GetCurrentUser are independent of each other. AcceptInvitation depends on InviteUserToBudget (creates the Invitation records AcceptInvitation reads). All slices require Infrastructure group to be complete.

Slices 2.1 and 2.2 can run in parallel after Infrastructure is done.
Slices 2.3, 2.4, 2.5 can run in parallel after 2.1 and 2.2.
Slices 2.6 and 2.7 can run in parallel; 2.6 must complete before 2.7 in integration testing order.

---

### [x] 2.1 RegisterUser slice
**Files**:
- `Project/src/MyBudget.Features/Features/Auth/RegisterUser/RegisterUserCommand.cs`
- `Project/src/MyBudget.Features/Features/Auth/RegisterUser/RegisterUserValidator.cs`
- `Project/src/MyBudget.Features/Features/Auth/RegisterUser/RegisterUserHandler.cs`
- `Project/src/MyBudget.Features/Features/Auth/RegisterUser/RegisterUserEndpoint.cs`
- `Project/src/MyBudget.Features/Features/Auth/RegisterUser/RegisterUser.resx` (default/en)
- `Project/src/MyBudget.Features/Features/Auth/RegisterUser/RegisterUser.es.resx`
**What**: Implement `POST /api/auth/register` — validate fields (email unique case-insensitive, password min 8/max 72/1 upper/1 lower/1 digit, firstName/lastName max 100, preferredLocale `"en"`|`"es"`), hash password with BCrypt workFactor 12, create `User` + `Budget` (name `"{firstName}'s Budget"`) + `BudgetMembership(Role=Owner)` in one EF transaction, generate JWT pair via `JwtTokenService`, hash refresh token with BCrypt workFactor 6, store in `RefreshTokens`, return `201` with `LoginResponse` + user profile; return `409` for duplicate email (`AUTH_EMAIL_TAKEN`), `422` for validation errors.
**Verify**: `POST /api/auth/register` returns `201` with `accessToken`, `refreshToken`, and user fields on valid input; returns `409` for duplicate email; returns `422` for weak password, missing fields, or unsupported locale; `User`, `Budget`, and `BudgetMembership` rows exist in DB after success.

---

### [x] 2.2 LoginUser slice
**Files**:
- `Project/src/MyBudget.Features/Features/Auth/LoginUser/LoginUserCommand.cs`
- `Project/src/MyBudget.Features/Features/Auth/LoginUser/LoginUserValidator.cs`
- `Project/src/MyBudget.Features/Features/Auth/LoginUser/LoginUserHandler.cs`
- `Project/src/MyBudget.Features/Features/Auth/LoginUser/LoginUserEndpoint.cs`
- `Project/src/MyBudget.Features/Features/Auth/LoginUser/LoginUser.resx`
- `Project/src/MyBudget.Features/Features/Auth/LoginUser/LoginUser.es.resx`
**What**: Implement `POST /api/auth/login` — Dapper SELECT user by email (case-insensitive), `BCrypt.Verify(plain, hash)`, update `LastLoginAt` via EF, generate JWT pair, BCrypt-hash raw refresh token (workFactor 6), insert into `RefreshTokens`, return `200` with `LoginResponse` + user profile; return `401 AUTH_INVALID_CREDENTIALS` for wrong credentials or unknown email (no enumeration — identical response either way).
**Verify**: `POST /api/auth/login` returns `200` with valid tokens on correct credentials; returns `401` for wrong password and for unknown email; `LastLoginAt` is updated in DB; `RefreshTokens` table gains a new row.

---

### [x] 2.3 RefreshToken slice
**Files**:
- `Project/src/MyBudget.Features/Features/Auth/RefreshToken/RefreshTokenCommand.cs`
- `Project/src/MyBudget.Features/Features/Auth/RefreshToken/RefreshTokenValidator.cs`
- `Project/src/MyBudget.Features/Features/Auth/RefreshToken/RefreshTokenHandler.cs`
- `Project/src/MyBudget.Features/Features/Auth/RefreshToken/RefreshTokenEndpoint.cs`
- `Project/src/MyBudget.Features/Features/Auth/RefreshToken/RefreshToken.resx`
- `Project/src/MyBudget.Features/Features/Auth/RefreshToken/RefreshToken.es.resx`
**What**: Implement `POST /api/auth/refresh` — accept `{ refreshToken, userId }` body; Dapper SELECT active tokens for `userId`; `BCrypt.Verify` each candidate until a match is found; if the matched token has `RevokedAt != null`, walk the `ReplacedByTokenId` family chain and revoke all tokens (`RevokedAt = now`), return `401 AUTH_REFRESH_TOKEN_REUSE`; if expired, return `401 AUTH_REFRESH_TOKEN_EXPIRED`; if no match, return `401 AUTH_REFRESH_TOKEN_INVALID`; on valid token: set `old.RevokedAt`, `old.ReplacedByTokenId = newId`, insert new refresh token, generate new access token, return `200` with new pair.
**Verify**: Valid token returns `200` with a new pair and the old token is marked revoked; reuse of a revoked token returns `401 AUTH_REFRESH_TOKEN_REUSE` and entire family is revoked; expired token returns `AUTH_REFRESH_TOKEN_EXPIRED`; unknown token returns `AUTH_REFRESH_TOKEN_INVALID`.

---

### [x] 2.4 LogoutUser slice
**Files**:
- `Project/src/MyBudget.Features/Features/Auth/LogoutUser/LogoutUserCommand.cs`
- `Project/src/MyBudget.Features/Features/Auth/LogoutUser/LogoutUserValidator.cs`
- `Project/src/MyBudget.Features/Features/Auth/LogoutUser/LogoutUserHandler.cs`
- `Project/src/MyBudget.Features/Features/Auth/LogoutUser/LogoutUserEndpoint.cs`
**What**: Implement `POST /api/auth/logout` with `[Authorize]` — accept `{ refreshToken }` body; find the matching `RefreshToken` record for the authenticated user via Dapper + `BCrypt.Verify`; if found, set `RevokedAt = now` via EF; if not found or already revoked, return `200` anyway (idempotent); unauthenticated requests rejected by middleware with `401`.
**Verify**: Authenticated logout sets `RevokedAt` on the matching `RefreshToken` row; calling again with the same token returns `200` (idempotent); unauthenticated request returns `401`.

---

### [x] 2.5 GetCurrentUser slice
**Files**:
- `Project/src/MyBudget.Features/Features/Auth/GetCurrentUser/GetCurrentUserQuery.cs`
- `Project/src/MyBudget.Features/Features/Auth/GetCurrentUser/GetCurrentUserHandler.cs`
- `Project/src/MyBudget.Features/Features/Auth/GetCurrentUser/GetCurrentUserEndpoint.cs`
**What**: Implement `GET /api/auth/me` with `[Authorize]` — Dapper-only read (no EF); extract `userId` from `ClaimTypes.NameIdentifier`; SELECT user profile + budget memberships via JOIN; return `200` with `CurrentUserResponse(id, email, firstName, lastName, preferredLocale, lastLoginAt, createdAt, memberships)`; no Validator file (query slice).
**Verify**: `GET /api/auth/me` returns `200` with correct user fields and membership list for authenticated user; returns `401` for expired/missing JWT.

---

### [x] 2.6 InviteUserToBudget slice
**Files**:
- `Project/src/MyBudget.Features/Features/Budgets/InviteUserToBudget/InviteUserToBudgetCommand.cs`
- `Project/src/MyBudget.Features/Features/Budgets/InviteUserToBudget/InviteUserToBudgetValidator.cs`
- `Project/src/MyBudget.Features/Features/Budgets/InviteUserToBudget/InviteUserToBudgetHandler.cs`
- `Project/src/MyBudget.Features/Features/Budgets/InviteUserToBudget/InviteUserToBudgetEndpoint.cs`
- `Project/src/MyBudget.Features/Features/Budgets/InviteUserToBudget/InviteUserToBudget.resx`
- `Project/src/MyBudget.Features/Features/Budgets/InviteUserToBudget/InviteUserToBudget.es.resx`
**What**: Implement `POST /api/budgets/{id}/invitations` with `[Authorize(Policy="budget:admin")]` — validate `email` (required, valid format, max 254) and `role` (one of admin/operator/read-only; `owner` is forbidden → `422 AUTH_CANNOT_INVITE_AS_OWNER`); Dapper-check budget exists (`404 BUDGET_NOT_FOUND`) and invitee not already a member (`409 AUTH_ALREADY_MEMBER`); generate 256-bit random token via `RandomNumberGenerator.GetBytes(32)`, BCrypt-hash it (workFactor 6) for `Invitation.TokenHash`, persist `Invitation` (ExpiresAt = now+72h); write `EmailMessage` to `IEmailSender` channel with invitation link `{FrontendBaseUrl}/invitations/accept?token={rawToken}`; evict `IMemoryCache` key for that budget+user if applicable; return `201` with `{ invitationId, expiresAt }`.
**Verify**: `POST /api/budgets/{id}/invitations` as admin returns `201`; `operator` caller returns `403`; `role=owner` returns `422`; duplicate member returns `409`; unknown budget returns `404`; invitation email appears in Mailpit; `Invitations` table has new row with hashed token.

---

### [x] 2.7 AcceptInvitation slice
**Files**:
- `Project/src/MyBudget.Features/Features/Auth/AcceptInvitation/AcceptInvitationCommand.cs`
- `Project/src/MyBudget.Features/Features/Auth/AcceptInvitation/AcceptInvitationValidator.cs`
- `Project/src/MyBudget.Features/Features/Auth/AcceptInvitation/AcceptInvitationHandler.cs`
- `Project/src/MyBudget.Features/Features/Auth/AcceptInvitation/AcceptInvitationEndpoint.cs`
- `Project/src/MyBudget.Features/Features/Auth/AcceptInvitation/AcceptInvitation.resx`
- `Project/src/MyBudget.Features/Features/Auth/AcceptInvitation/AcceptInvitation.es.resx`
**What**: Implement `POST /api/auth/invitations/accept` with `[Authorize]` — accept `{ token }` body; Dapper SELECT all `Invitation` records where `UsedAt IS NULL`; `BCrypt.Verify(rawToken, candidate.TokenHash)` to find match; if no match → `404 AUTH_INVITATION_NOT_FOUND`; if `ExpiresAt < now` → `410 AUTH_INVITATION_EXPIRED`; if `UsedAt != null` → `410 AUTH_INVITATION_ALREADY_USED`; if `Invitation.InviteeEmail != currentUser.Email` (case-insensitive) → `403 AUTH_INVITATION_EMAIL_MISMATCH`; on success: set `Invitation.UsedAt = now` via EF, create `BudgetMembership(BudgetId, UserId, Role)`, evict `IMemoryCache` key `"budget-membership:{userId}:{budgetId}"`; return `200` with `{ budgetId, role }`.
**Verify**: Valid authenticated invite accept returns `200` with `budgetId` and `role`; expired token returns `410`; already-used token returns `410`; email mismatch returns `403`; unknown token returns `404`; `BudgetMemberships` table gains new row after success; cache is evicted.

---

## Frontend

Tasks 3.1 and 3.2 can run in parallel (store and i18n are independent).
Tasks 3.3–3.7 depend on 3.1 (store must exist before views use it) and 3.2 (i18n keys must exist before views reference them).
Task 3.4 (router) can run in parallel with 3.5–3.7 (view creation) but views need the routes to be registered for deep-link testing.

---

### [x] 3.1 Rewrite auth.store.ts
**Files**: `Project/frontend/src/stores/auth.store.ts`
**What**: Full rewrite — add `User` interface (`id`, `email`, `firstName`, `lastName`, `memberships: BudgetMembershipDto[]`), `BudgetMembershipDto` interface; state: `user: User | null`, `accessToken: string | null`, `isAuthenticated: boolean`; actions: `login(email, password)` → POST `/api/auth/login` → store `accessToken` in state + `localStorage`, store `refreshToken` in `localStorage`, call `fetchMe()`; `register(payload)` → POST `/api/auth/register`; `logout()` → POST `/api/auth/logout` with `refreshToken`, clear state + `localStorage`; `refresh()` → POST `/api/auth/refresh` with `refreshToken` from `localStorage`, update `accessToken`; `fetchMe()` → GET `/api/auth/me` → set `user`; restore `accessToken` from `localStorage` on store initialization.
**Verify**: `login()` populates `user`, `accessToken`, and `isAuthenticated`; `logout()` clears all state and `localStorage`; `refresh()` updates token without clearing user; `isAuthenticated` is `true` after login and `false` after logout.

---

### [x] 3.2 Add auth and invitation i18n keys to en.json and es.json
**Files**:
- `Project/frontend/src/i18n/locales/en.json`
- `Project/frontend/src/i18n/locales/es.json`
**What**: Extend both locale files with all keys required by the spec — `auth.login.*` keys (title, emailPlaceholder, passwordPlaceholder, submit, registerLink, error.invalidCredentials), `auth.register.*` keys (title, emailPlaceholder, passwordPlaceholder, firstNamePlaceholder, lastNamePlaceholder, submit, loginLink, successMessage), `invitation.modal.*` keys (title, emailLabel, roleLabel, submit, successMessage, error.alreadyMember), `invitation.accept.*` keys (title, loading, successMessage, error.expired, error.alreadyUsed, error.mismatch).
**Verify**: Both JSON files are valid; all spec-required i18n keys are present in both `en.json` and `es.json`; no existing keys are removed.

---

### [x] 3.3 Add Axios 401 interceptor with token refresh retry
**Files**: `Project/frontend/src/api/axios.ts`
**What**: Add a response interceptor to the existing `http` instance — on `401`: if not already retrying, call `authStore.refresh()` then retry the original request with the new `accessToken` from store; if refresh fails or already retrying, call `authStore.logout()` and redirect to `/login`; the request interceptor already attaches `Authorization: Bearer {token}` but needs to read from `authStore.accessToken` (rename `authStore.token` → `authStore.accessToken` consistently).
**Verify**: A request that returns `401` is retried once after a successful refresh; a request that returns `401` after refresh failure triggers logout and redirects to `/login`; infinite retry loops are prevented.

---

### [x] 3.4 Add /register and /invitations/accept routes to router
**Files**: `Project/frontend/src/router/index.ts`
**What**: Add two new route records: `{ path: '/register', name: 'Register', component: () => import('@/views/RegisterView.vue'), meta: { public: true } }` and `{ path: '/invitations/accept', name: 'AcceptInvitation', component: () => import('@/views/AcceptInvitationView.vue'), meta: { public: true } }`; both routes must bypass the existing auth guard because users may not yet be authenticated when visiting them.
**Verify**: Navigating to `/register` renders `RegisterView.vue`; navigating to `/invitations/accept?token=xxx` renders `AcceptInvitationView.vue`; unauthenticated users are NOT redirected to `/login` for these routes.

---

### [x] 3.5 Create RegisterView.vue
**Files**: `Project/frontend/src/views/RegisterView.vue`
**What**: Create a registration form view with fields for `email`, `password`, `firstName`, `lastName`, and optional `preferredLocale` selector (`"en"` / `"es"`); call `authStore.register(payload)` on submit; show server validation errors mapped to field level; on success redirect to `/` (home); all user-visible strings use `$t()` with keys from 3.2; sanitize any rendered dynamic content with DOMPurify; use TailwindCSS + daisyUI components.
**Verify**: Form submits and calls `authStore.register()`; server 422 errors display next to the correct field; success redirects to `/`; no `v-html` without DOMPurify.

---

### [x] 3.6 Create AcceptInvitationView.vue
**Files**: `Project/frontend/src/views/AcceptInvitationView.vue`
**What**: Create a view that reads `?token=` from the route query on mount; if the user is already authenticated, immediately calls `POST /api/auth/invitations/accept` with `{ token }`; if not authenticated, redirects to `/login?redirect=/invitations/accept&token={token}` so the user can log in first then be sent back; on success show `invitation.accept.successMessage` and link to the accepted budget; on error show the appropriate error message from i18n keys; use `$t()` for all strings.
**Verify**: Authenticated user landing on `/invitations/accept?token=xxx` calls the accept endpoint; expired token shows the expired error message; unauthenticated user is redirected to login with the redirect URL preserved.

---

### [x] 3.7 Create InviteUserModal.vue
**Files**: `Project/frontend/src/components/budget/InviteUserModal.vue`
**What**: Create a modal component that accepts `budgetId: string` as a prop, presents an email input and a role selector (options: `admin`, `operator`, `read-only`), calls `POST /api/budgets/{budgetId}/invitations` on submit, emits `'invited'` on success so the parent can refresh the member list, shows server errors inline (409 already-member, 422 cannot-invite-as-owner); use `$t()` with `invitation.modal.*` keys; validate email format client-side with Zod before submitting; no `v-html` without DOMPurify.
**Verify**: Submitting with valid email and role calls the API and emits `'invited'`; already-member error displays inline; email format is validated before the API call is made.

---

## Backend Unit Tests

All 4.x tasks depend on the corresponding slice being complete. Tasks 4.1–4.2 can run after Infrastructure; 4.3–4.8 each depend on their respective slice.

---

### [x] 4.1 Unit tests: JwtTokenService
**Files**: `Project/tests/MyBudget.Features.Tests/SharedKernel/Auth/JwtTokenServiceTests.cs`
**What**: Test `GenerateAccessToken` — verify JWT contains exactly the five required claims (`sub`, `email`, `jti`, `iat`, `exp`), is signed with `HmacSha256`, and `exp` is ~15 min from now; test `GenerateRefreshToken` — verify output is Base64Url-decodable to 64 bytes and two consecutive calls produce different values. Use `NSubstitute` to mock `IOptions<JwtOptions>`. Assertions with `Shouldly`.
**Verify**: All tests pass; no real HTTP/DB calls; tests are deterministic.

---

### [x] 4.2 Unit tests: BudgetAuthorizationHandler
**Files**: `Project/tests/MyBudget.Features.Tests/SharedKernel/Auth/BudgetAuthorizationHandlerTests.cs`
**What**: Test `HandleRequirementAsync` — (a) cache hit with sufficient role → `Succeed`; (b) cache miss, DB row with sufficient role → `Succeed` + cache populated; (c) DB row with insufficient role → `Fail`; (d) no DB row → `Fail`; (e) missing `userId` claim → `Fail`; (f) missing `budgetId` route value → `Fail`. Mock `IMemoryCache`, `IDbConnection` (Dapper), and `IAuthorizationHandlerContext` with `NSubstitute`.
**Verify**: All six scenarios pass; cache is populated on DB hit; no real DB calls.

---

### [x] 4.3 Unit tests: RegisterUser validator
**Files**: `Project/tests/MyBudget.Features.Tests/Features/Auth/RegisterUser/RegisterUserValidatorTests.cs`
**What**: Test `RegisterUserValidator` — valid payload passes; email missing → fails; email invalid format → fails; password < 8 chars → fails; password > 72 chars → fails; password no uppercase → fails; password no digit → fails; firstName > 100 chars → fails; preferredLocale `"fr"` → fails; preferredLocale `"en"` and `"es"` → passes.
**Verify**: All cases pass; no DB or HTTP calls.

---

### [x] 4.4 Unit tests: LoginUser validator
**Files**: `Project/tests/MyBudget.Features.Tests/Features/Auth/LoginUser/LoginUserValidatorTests.cs`
**What**: Test `LoginUserValidator` — valid payload passes; email empty → fails; email invalid format → fails; password empty → fails.
**Verify**: All cases pass.

---

### [x] 4.5 Unit tests: RefreshToken validator
**Files**: `Project/tests/MyBudget.Features.Tests/Features/Auth/RefreshToken/RefreshTokenValidatorTests.cs`
**What**: Test `RefreshTokenValidator` — valid payload passes; `refreshToken` empty → fails; `userId` empty → fails.
**Verify**: All cases pass.

---

### [x] 4.6 Unit tests: InviteUserToBudget validator
**Files**: `Project/tests/MyBudget.Features.Tests/Features/Budgets/InviteUserToBudget/InviteUserToBudgetValidatorTests.cs`
**What**: Test `InviteUserToBudgetValidator` — valid email + valid role passes; email empty → fails; email > 254 chars → fails; invalid email format → fails; `role = "owner"` → fails with `AUTH_CANNOT_INVITE_AS_OWNER`; `role = "admin"` / `"operator"` / `"read-only"` → passes; unknown role → fails.
**Verify**: All cases pass.

---

## Backend Integration Tests

All 5.x tasks depend on Infrastructure + corresponding slice + respective unit tests. Use `Microsoft.AspNetCore.Mvc.Testing` with a real test Postgres DB (from Docker Compose). Tasks 5.1 and 5.2 can run in parallel; 5.3–5.5 depend on 5.1 and 5.2; 5.6 depends on 5.1; 5.7 depends on 5.6.

---

### [x] 5.1 Integration tests: RegisterUser endpoint
**Files**: `Project/tests/MyBudget.Integration.Tests/Features/Auth/RegisterUserTests.cs`
**What**: `POST /api/auth/register` — (a) valid payload → `201` with `accessToken`, `refreshToken`, user fields; (b) duplicate email → `409 AUTH_EMAIL_TAKEN`; (c) weak password → `422`; (d) missing firstName → `422`; (e) unsupported locale `"fr"` → `422`; (f) verify `Users`, `Budgets`, `BudgetMemberships` rows exist after success; (g) verify refresh token row exists in `RefreshTokens`.
**Verify**: All scenarios return expected status + body; DB state matches after success.

---

### [x] 5.2 Integration tests: LoginUser endpoint
**Files**: `Project/tests/MyBudget.Integration.Tests/Features/Auth/LoginUserTests.cs`
**What**: `POST /api/auth/login` — (a) correct credentials → `200` with token pair; (b) wrong password → `401` (no body enumeration difference); (c) unknown email → `401` (same response shape as wrong password); (d) verify `LastLoginAt` updated in DB; (e) verify new `RefreshTokens` row after success.
**Verify**: All scenarios pass; no email enumeration leak.

---

### [x] 5.3 Integration tests: RefreshToken endpoint
**Files**: `Project/tests/MyBudget.Integration.Tests/Features/Auth/RefreshTokenTests.cs`
**What**: `POST /api/auth/refresh` — (a) valid token → `200` with new pair, old token revoked; (b) reuse revoked token → `401 AUTH_REFRESH_TOKEN_REUSE`, entire family revoked; (c) expired token → `401 AUTH_REFRESH_TOKEN_EXPIRED`; (d) unknown token → `401 AUTH_REFRESH_TOKEN_INVALID`.
**Verify**: All four paths pass; DB state reflects revocation correctly.

---

### [x] 5.4 Integration tests: LogoutUser + GetCurrentUser endpoints
**Files**: `Project/tests/MyBudget.Integration.Tests/Features/Auth/LogoutAndMeTests.cs`
**What**: LogoutUser — (a) authenticated logout → `200`, `RevokedAt` set on token; (b) second logout same token → `200` (idempotent); (c) unauthenticated → `401`. GetCurrentUser — (a) valid JWT → `200` with correct profile + memberships; (b) expired JWT → `401`; (c) no header → `401`.
**Verify**: All scenarios pass.

---

### [x] 5.5 Integration tests: BudgetAuthorizationHandler policies
**Files**: `Project/tests/MyBudget.Integration.Tests/SharedKernel/Auth/BudgetAuthorizationTests.cs`
**What**: Using a seeded DB with known memberships — (a) `Owner` calling `budget:admin` policy endpoint → `200`; (b) `Admin` calling `budget:admin` → `200`; (c) `Operator` calling `budget:admin` → `403`; (d) `ReadOnly` calling `budget:operator` → `403`; (e) `ReadOnly` calling `budget:read` → `200`; (f) unauthenticated → `401`. Use `InviteUserToBudget` endpoint as the test target.
**Verify**: All role combinations return expected status codes.

---

### [x] 5.6 Integration tests: InviteUserToBudget endpoint
**Files**: `Project/tests/MyBudget.Integration.Tests/Features/Budgets/InviteUserToBudgetTests.cs`
**What**: `POST /api/budgets/{id}/invitations` — (a) admin caller → `201` with `invitationId` + `expiresAt`; (b) operator caller → `403`; (c) `role=owner` → `422 AUTH_CANNOT_INVITE_AS_OWNER`; (d) already-member invitee → `409 AUTH_ALREADY_MEMBER`; (e) unknown budget → `404 BUDGET_NOT_FOUND`; (f) verify `Invitations` row exists with hashed token; (g) verify email appears in Mailpit via HTTP.
**Verify**: All scenarios pass; token in DB is hashed (not raw).

---

### [x] 5.7 Integration tests: AcceptInvitation endpoint
**Files**: `Project/tests/MyBudget.Integration.Tests/Features/Auth/AcceptInvitationTests.cs`
**What**: `POST /api/auth/invitations/accept` — (a) valid token + matching user → `200` with `budgetId` + `role`; (b) expired token → `410 AUTH_INVITATION_EXPIRED`; (c) already-used token → `410 AUTH_INVITATION_ALREADY_USED`; (d) email mismatch → `403 AUTH_INVITATION_EMAIL_MISMATCH`; (e) unknown token → `404 AUTH_INVITATION_NOT_FOUND`; (f) unauthenticated → `401`; (g) verify `BudgetMemberships` row after success; (h) verify cache eviction occurs.
**Verify**: All scenarios pass; DB state correct after success.

---

## Frontend Tests

Tasks 6.1 and 6.2 can run in parallel. Tasks 6.3–6.5 depend on 6.1 (store must exist). Use Vitest + `@testing-library/vue`.

---

### [x] 6.1 Unit tests: auth.store
**Files**: `Project/frontend/src/stores/__tests__/auth.store.test.ts`
**What**: Test all store actions with mocked Axios — `login()`: verify `accessToken` set in state + `localStorage`, `isAuthenticated = true`, `fetchMe()` called; `logout()`: verify state cleared + `localStorage` cleared; `refresh()`: verify `accessToken` updated without clearing `user`; `fetchMe()`: verify `user` populated from response; `register()`: verify API call made with correct payload; store init: verify `accessToken` restored from `localStorage` on creation.
**Verify**: All actions pass; no real HTTP calls; `localStorage` behavior verified with `vi.spyOn`.

---

### [x] 6.2 Unit tests: Axios 401 interceptor
**Files**: `Project/frontend/src/api/__tests__/axios.test.ts`
**What**: Test response interceptor — (a) `401` on first attempt + successful refresh → original request retried with new token, returns `200`; (b) `401` on first attempt + refresh fails → `authStore.logout()` called + redirect to `/login`; (c) `401` while already retrying → no infinite loop, logout triggered. Mock Axios adapter with `vi.fn()`; mock `authStore.refresh()` and `authStore.logout()`.
**Verify**: All three scenarios pass; no infinite retry loop possible.

---

### [x] 6.3 Component tests: RegisterView
**Files**: `Project/frontend/src/views/__tests__/RegisterView.test.ts`
**What**: Render `RegisterView` with `@testing-library/vue` — (a) submit valid form → `authStore.register()` called with correct payload; (b) server returns `422` with field errors → errors displayed next to correct fields; (c) success → router pushed to `/`; (d) no `v-html` usage (static check); (e) all visible strings come from i18n keys (spot-check two labels).
**Verify**: All cases pass; no XSS vectors in rendered output.

---

### [x] 6.4 Component tests: AcceptInvitationView
**Files**: `Project/frontend/src/views/__tests__/AcceptInvitationView.test.ts`
**What**: Render `AcceptInvitationView` — (a) authenticated user + valid token → accept API called on mount, success message shown; (b) authenticated user + expired token → expired error message shown; (c) unauthenticated user → redirected to `/login?redirect=...` with token preserved in query; (d) email mismatch → mismatch error shown.
**Verify**: All four scenarios pass; redirect URL includes token.

---

### [x] 6.5 Component tests: InviteUserModal
**Files**: `Project/frontend/src/components/budget/__tests__/InviteUserModal.test.ts`
**What**: Render `InviteUserModal` with `budgetId` prop — (a) valid email + role → API called, `'invited'` event emitted; (b) invalid email format (client-side Zod) → API NOT called, inline error shown; (c) server returns `409 AUTH_ALREADY_MEMBER` → inline error shown; (d) server returns `422 AUTH_CANNOT_INVITE_AS_OWNER` → inline error shown.
**Verify**: All four scenarios pass; Zod validation prevents unnecessary API calls.

---

## E2E Tests

Tasks 7.1–7.3 depend on all Frontend tasks (3.x) and all Backend Slices (2.x) being complete. Run against Docker Compose stack (full app running). Use `@playwright/test`. Happy-path only — edge cases covered by integration tests.

---

### [x] 7.1 E2E: Register → auto-login → home
**Files**: `Project/frontend/e2e/auth/register.spec.ts`
**What**: Navigate to `/register`; fill in valid email, password, firstName, lastName; submit; assert redirect to `/` (home); assert `localStorage` contains `accessToken` and `refreshToken`; assert user display name visible on home page.
**Verify**: Test passes against running Docker Compose stack; no console errors.

---

### [x] 7.2 E2E: Login → logout → token refresh
**Files**: `Project/frontend/e2e/auth/login-logout.spec.ts`
**What**: (a) Login flow — navigate to `/login`, submit valid credentials, assert redirect to `/`, assert authenticated state in UI; (b) Logout flow — click logout, assert redirect to `/login`, assert `localStorage` cleared; (c) Token refresh — seed a near-expired access token in `localStorage`, make an authenticated navigation, assert silent refresh occurs (new token in `localStorage`) without user seeing a login redirect.
**Verify**: All three sub-flows pass; no login redirect flash on silent refresh.

---

### [x] 7.3 E2E: Invite flow — admin invites → invitee accepts → budget accessible
**Files**: `Project/frontend/e2e/auth/invite-accept.spec.ts`
**What**: (a) Admin user logs in, opens `InviteUserModal`, submits valid email + role `operator`, asserts `201` response and modal closes; (b) Verify invitation email in Mailpit via HTTP API, extract raw token from link; (c) Invitee registers (or logs in if already registered), navigates to `/invitations/accept?token={rawToken}`, asserts success message and budget listed in memberships; (d) Invitee navigates to budget — assert access granted at `operator` level.
**Verify**: Full invite-accept round-trip passes end-to-end; `BudgetMemberships` row confirmed via GET `/api/auth/me` response.

---

## Dependency Graph (summary)

```
1.1 (BCrypt pkg)         ─┐
1.2 (JwtBearer pkg)      ─┤
1.3 (JwtOptions + cfg)   ─┼──► 1.5 (entities) ──► 1.6 (EF configs) ──► 1.7 (DbSets)
1.4 (User Secrets)       ─┤                                                     │
1.14 (FrontendBaseUrl)   ─┘                                                     ▼
                                                                       1.8 (migration)
1.3 ──► 1.10 (JwtTokenService)
1.5 + 1.10 ──► 1.11 (ServiceCollectionExtensions)
1.5 ──► 1.12 (BudgetAuthorizationHandler)
1.2 + 1.3 + 1.12 ──► 1.13 (Program.cs wiring)

[All Infrastructure complete]
        │
        ├──► 2.1 (RegisterUser) ┐
        └──► 2.2 (LoginUser)   ─┤──► 2.3 (RefreshToken)
                                 ├──► 2.4 (LogoutUser)
                                 ├──► 2.5 (GetCurrentUser)
                                 └──► 2.6 (InviteUserToBudget) ──► 2.7 (AcceptInvitation)

3.1 (auth.store) ─┐
3.2 (i18n)       ─┼──► 3.3 (Axios interceptor)
                   ├──► 3.4 (router)
                   ├──► 3.5 (RegisterView)
                   ├──► 3.6 (AcceptInvitationView)
                   └──► 3.7 (InviteUserModal)

[Backend Unit Tests — after respective slice]
1.10 ──► 4.1 (JwtTokenService tests)
1.12 ──► 4.2 (BudgetAuthorizationHandler tests)
2.1  ──► 4.3 (RegisterUser validator tests)
2.2  ──► 4.4 (LoginUser validator tests)
2.3  ──► 4.5 (RefreshToken validator tests)
2.6  ──► 4.6 (InviteUserToBudget validator tests)

[Backend Integration Tests — after unit tests + slice]
4.3 + 2.1 ──► 5.1 (RegisterUser integration)
4.4 + 2.2 ──► 5.2 (LoginUser integration)
5.1 + 5.2  ──► 5.3 (RefreshToken integration)
5.1 + 5.2  ──► 5.4 (LogoutUser + Me integration)
5.1        ──► 5.5 (BudgetAuthorization policy integration)
4.6 + 2.6  ──► 5.6 (InviteUserToBudget integration)
5.6        ──► 5.7 (AcceptInvitation integration)

[Frontend Tests — after respective component + store]
3.1 ──► 6.1 (auth.store tests)
3.3 ──► 6.2 (Axios interceptor tests)
6.1 + 3.5 ──► 6.3 (RegisterView tests)
6.1 + 3.6 ──► 6.4 (AcceptInvitationView tests)
6.1 + 3.7 ──► 6.5 (InviteUserModal tests)

[E2E Tests — after all slices + all frontend tasks]
2.x + 3.x ──► 7.1 (Register E2E)
2.x + 3.x ──► 7.2 (Login/logout/refresh E2E)
5.6 + 3.7  ──► 7.3 (Invite-accept E2E)
```
