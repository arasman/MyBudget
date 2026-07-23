# Exploration: DB Isolation

**Change**: db-isolation
**Date**: 2026-07-23

---

## Summary

Integration tests are already isolated via `mybudget_test` with per-test row cleanup. E2E tests have no isolation — data accumulates in the dev DB with no teardown. Recommended fix: three-environment split (dev / testing / e2e) with `appsettings.E2E.json`, a guarded reset endpoint, and Playwright `globalSetup`/`globalTeardown`.

---

## Investigation Areas

### 1. Connection String Setup

- `appsettings.json` (API) has **no** `ConnectionStrings` key — dev string comes exclusively from User Secrets or env vars.
- `appsettings.Testing.json` exists: `"DefaultConnection": "...mybudget_test..."`.
- **No** `appsettings.E2E.json` or `appsettings.Production.json` anywhere.

### 2. ASPNETCORE_ENVIRONMENT Usage

Already in active use:
- `Program.cs` skips `MigrateAsync` when `Environment == "Testing"`.
- `AppDbContextFactory` reads it for design-time migration tooling.
- Standard ASP.NET Core config layering picks it up automatically.

### 3. Test Projects
