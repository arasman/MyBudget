# Design: Auth Feature

## Technical Approach

Seven VSA slices (4-file each) implement user registration, JWT login, refresh token rotation, logout, current-user profile, budget invitation, and invitation acceptance. Auth infrastructure (JWT configuration, BCrypt, per-budget authorization handler, IMemoryCache membership cache) lives in `SharedKernel/Auth/`. A single EF Core migration `AddAuthTables` introduces five new tables. The frontend auth store is wired end-to-end with an Axios 401 interceptor for silent token refresh.

---

## Architecture Decisions

### ADR-001: JWT (access) + rotating refresh token (not stateless-only, not cookie-based)

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Stateless JWT only | Simple, no DB reads on refresh; cannot revoke individual sessions | Rejected — TFM requires session revocation |
| Cookie-based sessions | Built-in CSRF protection; harder CORS setup, not REST-idiomatic for SPA | Rejected |
| JWT + rotating refresh (hashed in DB) | DB read on refresh path only; revocation + rotation possible | **Chosen** |

Access token: 15 min (configurable via `JWT__AccessTokenExpiryMinutes`). Refresh token: 7 days, single-use, stored as BCrypt hash in `RefreshTokens` table. On reuse of a revoked token, the entire family is revoked (walk `ReplacedByTokenId` chain).

### ADR-002: Roles NOT in JWT — custom IAuthorizationHandler reads DB per request

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Roles in JWT claims | Fast; stale role risk when membership changes mid-session | Rejected |
| DB read on every authorized request | Always fresh; N+1 risk without cache | **Chosen + mitigated by ADR-003** |

JWT payload: `sub` (userId), `email`, `jti`, `iat`, `exp` only. No role claims.

### ADR-003: IMemoryCache for BudgetMembership (TTL 5 min, keyed userId+budgetId)

| Option | Tradeoff | Decision |
|--------|----------|----------|
| No cache (raw Dapper per request) | Correct but N+1 on every route with `[Authorize(Policy="budget:read")]` | Rejected |
| IMemoryCache short-TTL | Small staleness window (max 5 min); eliminates repeated DB hits in same session | **Chosen** |
| Redis distributed cache | Overkill for TFM; Redis already in infra but not needed for membership | Rejected |

Cache key: `"budget-membership:{userId}:{budgetId}"`. TTL: 5 minutes. Evicted explicitly on `InviteUserToBudget` (new member added) and `AcceptInvitation` (membership created).

### ADR-004: Budget entity scoped into auth change

Budget is a required dependency for `BudgetMembership` and `Invitation` FK constraints. Including it in the auth migration (`AddAuthTables`) is correct: the Budget table did not exist before; it is not a patch to an existing table.

### ADR-005: localStorage for token storage (DOMPurify mitigates XSS at TFM scope)

| Option | Tradeoff | Decision |
|--------|----------|----------|
| HttpOnly cookie | Best XSS protection; requires same-origin or CORS config with credentials | Deferred post-TFM |
| localStorage | Accessible to JS (XSS risk); DOMPurify wired on all user-supplied content; acceptable at TFM scope | **Chosen** |

Access token stored in Pinia state (memory) and persisted via `localStorage` for page refresh recovery. Refresh token sent as `Authorization: Bearer` from store — NOT stored separately in localStorage.

---

## Data Flow

### Login flow

```
POST /api/auth/login
  → LoginUserEndpoint
    → IMediator.Send(LoginUserCommand)
      → LoginUserHandler
          1. Dapper: SELECT user by email
          2. BCrypt.Verify(plainPassword, user.PasswordHash)
          3. JwtTokenService.GenerateAccessToken(user)
          4. JwtTokenService.GenerateRefreshToken() → random 64 bytes
          5. BCrypt.HashPassword(rawRefreshToken) → store in RefreshTokens (EF)
          6. Return { accessToken, refreshToken, expiresIn }
  ← 200 { accessToken, refreshToken, expiresIn }
```

### Refresh flow

```
POST /api/auth/refresh { refreshToken }
  → RefreshTokenHandler
      1. Dapper: SELECT RefreshToken by hash match (BCrypt.Verify against all active tokens for userId)
         NOTE: lookup by UserId from unverified claim in body, then verify hash
      2. If token.RevokedAt != null → revoke entire family → 401
      3. Revoke old token (set RevokedAt, ReplacedByTokenId = newId)
      4. Create new RefreshToken
      5. Generate new AccessToken
      6. Return new pair
```

### Per-budget authorization flow

```
Request with JWT → UseAuthentication populates ClaimsPrincipal
  → [Authorize(Policy = "budget:read")] triggers BudgetAuthorizationHandler
      1. Extract budgetId from route values
      2. IMemoryCache.TryGetValue("budget-membership:{userId}:{budgetId}")
         → hit: use cached BudgetMembership
         → miss: Dapper SELECT from BudgetMemberships WHERE UserId + BudgetId → cache 5 min
      3. membership.Role >= requirement.MinimumRole → Succeed() else Fail()
```

---

## Package Additions

| Package | Version | Target .csproj | Reason |
|---------|---------|----------------|--------|
| `BCrypt.Net-Next` | `4.*` | `MyBudget.Features.csproj` | Used in RegisterUserHandler, LoginUserHandler, RefreshTokenHandler |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | `10.*` | `MyBudget.Api.csproj` | JWT middleware wired in Program.cs |

---

## JWT Configuration Design

**appsettings.json** (non-secret, safe to commit):
```json
"JWT": {
  "Issuer": "MyBudget",
  "Audience": "MyBudget.Client",
  "AccessTokenExpiryMinutes": 15
}
```

**User Secrets / env var** (never in appsettings.json):
- `JWT__Key` — minimum 32-char random string; Program.cs guard: `if (string.IsNullOrEmpty(opts.Key)) throw new InvalidOperationException("JWT:Key is not configured.")`

**Options record** (`SharedKernel/Auth/JwtOptions.cs`):
```csharp
public sealed record JwtOptions
{
    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int AccessTokenExpiryMinutes { get; init; } = 15;
}
```

Bound via: `services.Configure<JwtOptions>(config.GetSection("JWT"))` inside `AddFeatures`.

**JwtTokenService** (`SharedKernel/Auth/JwtTokenService.cs`) — registered as `Scoped` in `AddFeatures`:
- `string GenerateAccessToken(User user)` — creates `SecurityTokenDescriptor`, signs with `HmacSha256`
- `string GenerateRefreshToken()` — `RandomNumberGenerator.GetBytes(64)` → Base64Url

---

## Entity File Contracts

### Entities (SharedKernel/Entities/)

| Entity | File | Key Fields |
|--------|------|-----------|
| `User` | `SharedKernel/Entities/User.cs` | `Guid Id`, `string Email`, `string PasswordHash`, `string FirstName`, `string LastName`, `string PreferredLocale`, `DateTime? LastLoginAt`, `DateTime CreatedAt`, `DateTime UpdatedAt` |
| `RefreshToken` | `SharedKernel/Entities/RefreshToken.cs` | `Guid Id`, `Guid UserId`, `string TokenHash`, `DateTime ExpiresAt`, `DateTime? RevokedAt`, `Guid? ReplacedByTokenId` |
| `Budget` | `SharedKernel/Entities/Budget.cs` | `Guid Id`, `string Name`, `Guid OwnerId`, `DateTime CreatedAt` |
| `BudgetMembership` | `SharedKernel/Entities/BudgetMembership.cs` | `Guid Id`, `Guid BudgetId`, `Guid UserId`, `BudgetRole Role`, `DateTime JoinedAt` |
| `Invitation` | `SharedKernel/Entities/Invitation.cs` | `Guid Id`, `Guid BudgetId`, `string InviteeEmail`, `BudgetRole Role`, `string TokenHash`, `DateTime ExpiresAt`, `DateTime? UsedAt`, `Guid InvitedByUserId` |
| `BudgetRole` | `SharedKernel/Entities/BudgetRole.cs` | `enum: Owner=40, Admin=30, Operator=20, ReadOnly=10` — int values enable >= comparisons |

### EF Configurations (SharedKernel/Persistence/Configurations/)

| File | Key Config |
|------|-----------|
| `UserConfiguration.cs` | `HasKey(u => u.Id)`, `HasIndex(u => u.Email).IsUnique()`, `HasMany(u => u.RefreshTokens)`, `Property(u => u.Email).HasMaxLength(320)` |
| `RefreshTokenConfiguration.cs` | `HasOne<User>().WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade)`, `HasIndex(t => t.UserId)`, `HasIndex(t => new { t.UserId, t.RevokedAt })` |
| `BudgetConfiguration.cs` | `HasKey(b => b.Id)`, `HasOne<User>().WithMany().HasForeignKey(b => b.OwnerId).OnDelete(DeleteBehavior.Restrict)` |
| `BudgetMembershipConfiguration.cs` | `HasKey(m => m.Id)`, `HasIndex(m => new { m.BudgetId, m.UserId }).IsUnique()`, FK to Budget (Cascade), FK to User (Restrict) |
| `InvitationConfiguration.cs` | `HasKey(i => i.Id)`, `HasIndex(i => i.TokenHash).IsUnique()`, `HasIndex(i => i.InviteeEmail)`, FK to Budget (Cascade), FK to User as InvitedBy (Restrict), `HasConversion<int>()` on Role |

---

## Slice File Contracts

### Features/Auth/

```
Features/Auth/
├── RegisterUser/
│   ├── RegisterUserCommand.cs       record RegisterUserCommand(string Email, string Password, string FirstName, string LastName) : ICommand<Result<Guid>>
│   ├── RegisterUserValidator.cs     class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
│   ├── RegisterUserHandler.cs       class RegisterUserHandler : ICommandHandler<RegisterUserCommand, Result<Guid>>
│   └── RegisterUserEndpoint.cs      static Map(IEndpointRouteBuilder) → POST /api/auth/register
│
├── LoginUser/
│   ├── LoginUserCommand.cs          record LoginUserCommand(string Email, string Password) : ICommand<Result<LoginResponse>>
│   ├── LoginUserValidator.cs
│   ├── LoginUserHandler.cs
│   └── LoginUserEndpoint.cs         POST /api/auth/login
│
├── RefreshToken/
│   ├── RefreshTokenCommand.cs       record RefreshTokenCommand(string RefreshToken) : ICommand<Result<LoginResponse>>
│   ├── RefreshTokenValidator.cs
│   ├── RefreshTokenHandler.cs
│   └── RefreshTokenEndpoint.cs      POST /api/auth/refresh
│
├── LogoutUser/
│   ├── LogoutUserCommand.cs         record LogoutUserCommand(string RefreshToken) : ICommand<Result<Unit>>
│   ├── LogoutUserValidator.cs
│   ├── LogoutUserHandler.cs
│   └── LogoutUserEndpoint.cs        POST /api/auth/logout  [Authorize]
│
├── GetCurrentUser/
│   ├── GetCurrentUserQuery.cs       record GetCurrentUserQuery(Guid UserId) : IQuery<Result<CurrentUserResponse>>
│   ├── GetCurrentUserHandler.cs     IQueryHandler — Dapper only (read)
│   └── GetCurrentUserEndpoint.cs    GET /api/auth/me  [Authorize]
│
└── AcceptInvitation/
    ├── AcceptInvitationCommand.cs   record AcceptInvitationCommand(string Token, Guid UserId) : ICommand<Result<Unit>>
    ├── AcceptInvitationValidator.cs
    ├── AcceptInvitationHandler.cs   — writes BudgetMembership, sets Invitation.UsedAt, evicts cache
    └── AcceptInvitationEndpoint.cs  POST /api/auth/invitations/accept  [Authorize]
```

### Features/Budgets/

```
Features/Budgets/
└── InviteUserToBudget/
    ├── InviteUserToBudgetCommand.cs  record InviteUserToBudgetCommand(Guid BudgetId, string InviteeEmail, BudgetRole Role, Guid InvitedByUserId) : ICommand<Result<Guid>>
    ├── InviteUserToBudgetValidator.cs
    ├── InviteUserToBudgetHandler.cs  — creates Invitation, writes to EmailChannel, evicts cache
    └── InviteUserToBudgetEndpoint.cs POST /api/budgets/{id}/invitations  [Authorize(Policy="budget:admin")]
```

### Shared response types (SharedKernel/Auth/)

```
LoginResponse.cs    record LoginResponse(string AccessToken, string RefreshToken, int ExpiresIn)
CurrentUserResponse.cs  record CurrentUserResponse(Guid Id, string Email, string FirstName, string LastName, IReadOnlyList<BudgetMembershipDto> Memberships)
BudgetMembershipDto.cs  record BudgetMembershipDto(Guid BudgetId, string BudgetName, string Role)
```

---

## Per-Budget Authorization Design

**Files** (all in `SharedKernel/Auth/Authorization/`):

```
BudgetRole.cs                    — enum (already listed above; int values for >= comparison)
BudgetRequirement.cs             — record BudgetRequirement(BudgetRole MinimumRole) : IAuthorizationRequirement
BudgetAuthorizationHandler.cs    — class BudgetAuthorizationHandler : AuthorizationHandler<BudgetRequirement>
AuthorizationPolicyExtensions.cs — static void AddBudgetPolicies(this AuthorizationOptions opts)
```

**BudgetAuthorizationHandler logic:**

```
HandleRequirementAsync:
  1. userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier) → parse Guid
  2. budgetId = authContext.Resource as HttpContext → route values["id"] → parse Guid
  3. key = $"budget-membership:{userId}:{budgetId}"
  4. IMemoryCache.TryGetValue(key) → BudgetMembership?
     miss: Dapper SELECT WHERE UserId = @userId AND BudgetId = @budgetId
           → cache with AbsoluteExpirationRelativeToNow = 5 min
  5. (int)membership.Role >= (int)requirement.MinimumRole → Succeed() else Fail()
```

**Named policies:**

| Policy | MinimumRole | Used by |
|--------|-------------|---------|
| `"budget:admin"` | Admin (30) | InviteUserToBudget |
| `"budget:operator"` | Operator (20) | Future transaction endpoints |
| `"budget:read"` | ReadOnly (10) | GetCurrentUser memberships, future read endpoints |

**Registration** (added to `AddFeatures`):
```csharp
services.AddMemoryCache();
services.AddScoped<IAuthorizationHandler, BudgetAuthorizationHandler>();
// Named policies registered in MyBudget.Api/Program.cs via AddAuthorization(opts => opts.AddBudgetPolicies())
```

---

## Program.cs Changes (MyBudget.Api)

Replace stub `AddAuthentication()` / `AddAuthorization()` with:

```csharp
// JWT authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts => { /* bind from JwtOptions, validate issuer/audience/lifetime/key */ });

// Authorization with budget policies
builder.Services.AddAuthorization(opts => opts.AddBudgetPolicies());

// Startup guard — fail fast if JWT:Key missing
var jwtOpts = builder.Configuration.GetSection("JWT").Get<JwtOptions>()
    ?? throw new InvalidOperationException("JWT section is not configured.");
if (string.IsNullOrWhiteSpace(jwtOpts.Key))
    throw new InvalidOperationException("JWT:Key is not configured. Set via User Secrets or JWT__Key env var.");
```

---

## Refresh Token Rotation Design

1. **On login**: `RandomNumberGenerator.GetBytes(64)` → `Convert.ToBase64String` → raw token returned to client. `BCrypt.HashPassword(rawToken, workFactor: 12)` → stored in `RefreshTokens.TokenHash`.
2. **On refresh**: `SELECT * FROM RefreshTokens WHERE UserId = @userId AND RevokedAt IS NULL AND ExpiresAt > NOW()`. For each candidate: `BCrypt.Verify(rawToken, candidate.TokenHash)` → first match wins.
   - If match found but `RevokedAt != null` → token reuse detected → revoke entire family (recursive walk via `ReplacedByTokenId`) → return 401.
   - If match found and valid → begin rotation: set `old.RevokedAt = now`, `old.ReplacedByTokenId = newId`, insert new token.
3. **Family revocation**: walk `ReplacedByTokenId` chain forward to find all descendants; also walk backward from the presented token. Mark all `RevokedAt = now`.

> Note: BCrypt on refresh path is acceptable — refresh happens at most every 15 min, not per-request.

---

## Frontend Architecture

### auth.store.ts (rewrite)

```typescript
interface User { id: string; email: string; firstName: string; lastName: string; memberships: BudgetMembershipDto[] }
interface BudgetMembershipDto { budgetId: string; budgetName: string; role: string }

State: { user: User | null; accessToken: string | null; isAuthenticated: boolean }

Actions:
  login(email, password)     → POST /api/auth/login → store token, call fetchMe()
  register(payload)          → POST /api/auth/register
  logout()                   → POST /api/auth/logout → clear store + localStorage
  refresh()                  → POST /api/auth/refresh → update accessToken in store
  fetchMe()                  → GET /api/auth/me → populate user
```

localStorage persistence: `accessToken` and `refreshToken` stored on login; cleared on logout. Pinia `persist` plugin or manual `watch`.

### Axios interceptor (http/client.ts or plugins/axios.ts)

```
Request interceptor: attach Authorization: Bearer {accessToken} from store
Response interceptor:
  on 401:
    if (not already retrying):
      await authStore.refresh()
      retry original request with new token
    else:
      authStore.logout() → router.push('/login')
```

### Route guard additions (router/index.ts)

New public routes added:
- `/register` → `meta: { public: true }`
- `/invitations/accept` → `meta: { public: true }` (user may not be logged in yet)

Guard logic: unchanged (`if to.meta.requiresAuth && !authStore.isAuthenticated → /login`). Public routes bypass guard.

### New views/components

| Path | Type | Description |
|------|------|-------------|
| `views/RegisterView.vue` | View | Registration form — calls `authStore.register()` |
| `views/AcceptInvitationView.vue` | View | Reads `?token=` from query, calls `POST /api/auth/invitations/accept` |
| `components/budget/InviteUserModal.vue` | Component | Role selector + email input; emits `invited` on success; parent refreshes member list |

---

## Email Template Design

**Invitation email** sent by `InviteUserToBudgetHandler` via `IEmailSender.SendAsync(EmailMessage)`.

`EmailMessage` already supports `Subject`, `To`, `Body` (HTML string).

Handler constructs:
- Subject: `IStringLocalizer["Invitation.Subject"]` (en: "You've been invited to {BudgetName}")
- Body: HTML snippet with accept link: `{FrontendBaseUrl}/invitations/accept?token={rawToken}`
- `FrontendBaseUrl`: read from config `App__FrontendBaseUrl` (non-secret; in appsettings.json)
- Locale: use `invitee.PreferredLocale` if user exists, else default to `"en"`

**Locale fallback**: `IStringLocalizer` falls back to default resource if locale .resx not found — standard .NET behavior, no custom logic needed.

**IEmailSender**: already exists at `SharedKernel/Email/IEmailSender.cs` — no changes needed.

---

## Migration Design

**Migration name**: `AddAuthTables`

**Table creation order** (FK dependency order):

1. `Users` — no FK dependencies
2. `Budgets` — FK to Users (OwnerId)
3. `BudgetMemberships` — FK to Budgets + Users
4. `RefreshTokens` — FK to Users
5. `Invitations` — FK to Budgets + Users (InvitedByUserId)

**Indexes**:

| Table | Index | Type |
|-------|-------|------|
| Users | `IX_Users_Email` | Unique |
| RefreshTokens | `IX_RefreshTokens_UserId` | Non-unique |
| RefreshTokens | `IX_RefreshTokens_UserId_RevokedAt` | Composite (partial query optimization) |
| BudgetMemberships | `IX_BudgetMemberships_BudgetId_UserId` | Unique |
| Invitations | `IX_Invitations_TokenHash` | Unique |
| Invitations | `IX_Invitations_InviteeEmail` | Non-unique |

**Guard**: `InitialCreate` migration MUST NOT be touched. `AddAuthTables` is a new migration added after it.

---

## File Changes Summary

| File | Action | Description |
|------|--------|-------------|
| `MyBudget.Features.csproj` | Modify | Add `BCrypt.Net-Next 4.*` |
| `MyBudget.Api.csproj` | Modify | Add `Microsoft.AspNetCore.Authentication.JwtBearer 10.*` |
| `Program.cs` | Modify | Replace stub auth with JWT bearer + budget policies + startup guard |
| `Extensions/ServiceCollectionExtensions.cs` | Modify | Add `Configure<JwtOptions>`, `AddMemoryCache`, `AddScoped<BudgetAuthorizationHandler>`, `AddScoped<JwtTokenService>` |
| `SharedKernel/Auth/JwtOptions.cs` | Create | Options record bound from config |
| `SharedKernel/Auth/JwtTokenService.cs` | Create | Access + refresh token generation |
| `SharedKernel/Auth/Authorization/BudgetRole.cs` | Create | Enum with int values |
| `SharedKernel/Auth/Authorization/BudgetRequirement.cs` | Create | IAuthorizationRequirement record |
| `SharedKernel/Auth/Authorization/BudgetAuthorizationHandler.cs` | Create | Reads BudgetMembership from cache/DB |
| `SharedKernel/Auth/Authorization/AuthorizationPolicyExtensions.cs` | Create | Named policy registration |
| `SharedKernel/Auth/LoginResponse.cs` | Create | Shared response DTO |
| `SharedKernel/Auth/CurrentUserResponse.cs` | Create | Shared response DTO |
| `SharedKernel/Auth/BudgetMembershipDto.cs` | Create | Shared DTO |
| `SharedKernel/Entities/User.cs` | Create | Entity |
| `SharedKernel/Entities/RefreshToken.cs` | Create | Entity |
| `SharedKernel/Entities/Budget.cs` | Create | Entity |
| `SharedKernel/Entities/BudgetMembership.cs` | Create | Entity |
| `SharedKernel/Entities/Invitation.cs` | Create | Entity |
| `SharedKernel/Persistence/Configurations/UserConfiguration.cs` | Create | EF config |
| `SharedKernel/Persistence/Configurations/RefreshTokenConfiguration.cs` | Create | EF config |
| `SharedKernel/Persistence/Configurations/BudgetConfiguration.cs` | Create | EF config |
| `SharedKernel/Persistence/Configurations/BudgetMembershipConfiguration.cs` | Create | EF config |
| `SharedKernel/Persistence/Configurations/InvitationConfiguration.cs` | Create | EF config |
| `Features/Auth/RegisterUser/*.cs` (×4) | Create | VSA slice |
| `Features/Auth/LoginUser/*.cs` (×4) | Create | VSA slice |
| `Features/Auth/RefreshToken/*.cs` (×4) | Create | VSA slice |
| `Features/Auth/LogoutUser/*.cs` (×4) | Create | VSA slice |
| `Features/Auth/GetCurrentUser/*.cs` (×3) | Create | VSA slice (no Validator — query) |
| `Features/Auth/AcceptInvitation/*.cs` (×4) | Create | VSA slice |
| `Features/Budgets/InviteUserToBudget/*.cs` (×4) | Create | VSA slice |
| `Migrations/AddAuthTables.cs` | Create | EF migration |
| `appsettings.json` (Api) | Modify | Add JWT section (non-secret keys only) |
| `frontend/src/stores/auth.store.ts` | Modify | Full rewrite with User type, actions, localStorage |
| `frontend/src/router/index.ts` | Modify | Add /register and /invitations/accept public routes |
| `frontend/src/plugins/axios.ts` | Create | Axios instance + interceptors |
| `frontend/src/views/RegisterView.vue` | Create | Registration form |
| `frontend/src/views/AcceptInvitationView.vue` | Create | Invitation accept flow |
| `frontend/src/components/budget/InviteUserModal.vue` | Create | Invite user modal |

---

## Testing Strategy

TDD is off for this change. Manual integration testing via:
- Scalar UI (OpenAPI) for all 7 endpoints
- Mailpit (dev) to verify invitation emails
- Sequence: register → login → refresh → me → invite → accept → verify membership

---

## Open Questions

- [ ] Should `GetCurrentUser` return budget memberships inline or as a separate endpoint? (Current design: inline — acceptable for TFM scale)
- [ ] `BudgetRole.Owner` assignment: `RegisterUser` creates Budget + BudgetMembership with `Role=Owner` in same transaction — confirm this is the expected UX (no separate budget creation step)
- [ ] Frontend `persist` plugin for Pinia or manual localStorage watch? (Manual watch is simpler, no extra dependency)
