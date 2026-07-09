# Auth Feature — Implementation Tasks (ARCHIVED — ALL COMPLETE)

**Change**: auth
**Status**: COMPLETE — All 62 tasks marked complete on 2026-07-08
**Total tasks**: 62 (14 Infrastructure · 20 Backend Slices · 7 Frontend · 6 Backend Unit Tests · 7 Backend Integration Tests · 5 Frontend Tests · 3 E2E Tests)

This file is archived. For the full detailed task list with dependency graph and verification details, see the live version at openspec/changes/auth/tasks.md (before archiving) or refer to sdd/auth/tasks observation #122 in Engram.

## Task Groups Summary

### Infrastructure (14 tasks) — ALL COMPLETE
1.1–1.4: NuGet packages + JWT config + User Secrets
1.5–1.9: Entities, EF configs, DbSets, migration
1.10–1.14: JwtTokenService, service registration, authorization handler, wiring, config

### Backend Slices (20 tasks) — ALL COMPLETE
2.1–2.7: RegisterUser, LoginUser, RefreshToken, LogoutUser, GetCurrentUser, InviteUserToBudget, AcceptInvitation

### Frontend (7 tasks) — ALL COMPLETE
3.1–3.2: auth.store.ts rewrite, i18n keys
3.3–3.7: Axios interceptor, routes, RegisterView, AcceptInvitationView, InviteUserModal

### Backend Unit Tests (6 tasks) — ALL COMPLETE
4.1–4.6: JwtTokenService, BudgetAuthorizationHandler, validators for RegisterUser/LoginUser/RefreshToken/InviteUserToBudget

### Backend Integration Tests (7 tasks) — ALL COMPLETE
5.1–5.7: RegisterUser, LoginUser, RefreshToken, LogoutUser+Me, BudgetAuthorization, InviteUserToBudget, AcceptInvitation

### Frontend Tests (5 tasks) — ALL COMPLETE
6.1–6.5: auth.store, Axios interceptor, RegisterView, AcceptInvitationView, InviteUserModal

### E2E Tests (3 tasks) — ALL COMPLETE
7.1–7.3: Register→auto-login→home, Login→logout→refresh, Invite→accept→budget-access

## Verification Status

All 62 tasks verified complete:
- 112 tests passing (69 backend + 30 frontend + 9 E2E)
- Build: PASS
- No CRITICAL implementation bugs
- Test-coverage gaps acknowledged in verify-report

Archive created 2026-07-08 from Engram observation #122.
