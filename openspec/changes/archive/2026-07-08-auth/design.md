# Design: Auth Feature

## Technical Approach Summary

Seven VSA slices (4-file each) implement user registration, JWT login, refresh token rotation, logout, current-user profile, budget invitation, and invitation acceptance. Auth infrastructure lives in `SharedKernel/Auth/`. A single EF Core migration `AddAuthTables` introduces five new tables. The frontend auth store is wired end-to-end with an Axios 401 interceptor for silent token refresh.

## Key Architecture Decisions

1. **ADR-001**: JWT (15min access) + rotating refresh token (7 days, single-use, hashed in DB). Reuse detected → entire family revoked.
2. **ADR-002**: No roles in JWT. Custom BudgetAuthorizationHandler reads BudgetMembership from DB per request.
3. **ADR-003**: IMemoryCache TTL 5min keyed "budget-membership:{userId}:{budgetId}". Evicted on InviteUserToBudget + AcceptInvitation.
4. **ADR-004**: Budget entity scoped into auth migration (required FK dependency).
5. **ADR-005**: localStorage for tokens (DOMPurify mitigates XSS at TFM scope).

## Packages

- `BCrypt.Net-Next 4.*` → MyBudget.Features.csproj
- `Microsoft.AspNetCore.Authentication.JwtBearer 10.*` → MyBudget.Api.csproj

## Entities (5 new)

- User: Id, Email (unique), PasswordHash, FirstName, LastName, PreferredLocale, LastLoginAt, CreatedAt, UpdatedAt
- RefreshToken: Id, UserId (FK), TokenHash, ExpiresAt, RevokedAt, ReplacedByTokenId
- Budget: Id, Name, OwnerId (FK), CreatedAt
- BudgetMembership: Id, BudgetId (FK), UserId (FK), Role (Owner=40/Admin=30/Operator=20/ReadOnly=10), JoinedAt
- Invitation: Id, BudgetId (FK), InviteeEmail, Role, TokenHash, ExpiresAt, UsedAt, InvitedByUserId (FK)

## Slices (7 total)

Features/Auth/: RegisterUser, LoginUser, RefreshToken, LogoutUser, GetCurrentUser
Features/Budgets/: InviteUserToBudget
Features/Auth/: AcceptInvitation

## Per-Budget Authorization

BudgetRequirement(BudgetRole MinimumRole) : IAuthorizationRequirement
BudgetAuthorizationHandler: extracts budgetId from route, checks IMemoryCache → Dapper fallback
Policies: budget:admin (30), budget:operator (20), budget:read (10)

## Frontend

- auth.store.ts: User interface, login/register/logout/refresh/fetchMe actions, localStorage persistence
- Axios interceptor: 401 → refresh once → retry; on failure → logout + /login
- New public routes: /register, /invitations/accept
- New views: RegisterView, AcceptInvitationView
- New component: components/budget/InviteUserModal.vue

## Migration: AddAuthTables

Table order: Users → Budgets → BudgetMemberships → RefreshTokens → Invitations
Indexes: Email unique, UserId composites, TokenHash unique, InviteeEmail
InitialCreate MUST NOT be touched.

Full design details: See openspec/changes/archive/2026-07-08-auth/ or previous design phase artifact.
