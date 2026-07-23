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

| Project | Type | DB |
|---|---|---|
| `MyBudget.Features.Tests` | Unit | EF Core SQLite in-memory |
| `MyBudget.Integration.Tests` | Integration | Real Postgres (`mybudget_test`) |
| `Project/frontend/` | E2E (Playwright) | No dedicated DB — uses dev DB |

### 4. Integration Test DB

- `IntegrationTestFactory` hardcodes the connection string as a `const` — does **not** read `appsettings.Testing.json`.
- `InitializeAsync`: `EnsureDeletedAsync()` + `MigrateAsync()` — full schema wipe per collection.
- `IntegrationTestBase.InitializeAsync()`: `CleanDatabaseAsync()` — manual `RemoveRange` per entity set in FK order.
- **Integration tests are effectively isolated from dev DB. No data leak here.**

### 5. E2E / Playwright

- `playwright.config.ts`: no `webServer` block, no `globalSetup`, no `globalTeardown`.
- API assumed to be running manually via Docker Compose.
- E2E tests call `/api/auth/register` directly to seed users.
- **`ASPNETCORE_ENVIRONMENT=E2E` is never set. E2E data accumulates in whatever DB the running Docker API targets — in practice the dev DB. This is the primary contamination gap.**

### 6. EF Core / Migrations

- Migrations in `Project/src/MyBudget.Features/Migrations/`.
- `AppDbContext` registered in `ServiceCollectionExtensions.AddFeatures()`.
- `ConnectionFactory` (Dapper singleton) also reads `DefaultConnection`.
- Both converge on the same config key — a single env change switches both.

### 7. Docker / CI

- Single `postgres` service in docker-compose — one container, serves all.
- No CI workflow files at repo root.
- No committed `.env`; docker-compose references it via `env_file: .env`.

### 8. Current Pain

- `openspec/config.yaml` incorrectly documents integration tests as "SQLite in-memory override" — they actually hit real Postgres.
- E2E has no teardown. Dev DB accumulates test data indefinitely.

---

## Approaches

| Approach | Pros | Cons | Effort |
|---|---|---|---|
| **A — Minimal**: `appsettings.E2E.json` + DB naming only | Tiny diff | E2E contamination remains without teardown | Low |
| **B — Standard**: 3-env split + `globalSetup`/`globalTeardown` + guarded reset endpoint | Clean isolation for all 3 envs; follows existing patterns | Reset endpoint must be strictly gated; docker-compose init SQL | Medium |
| **C — Full**: Respawn + docker-compose test profile + CI pipeline | Fastest teardown; CI-ready | New library dep; more infra plumbing | High |

---

## Recommendation

**Approach B.**

1. Add `appsettings.E2E.json` with `mybudget_e2e` connection string.
2. Update `Program.cs` to skip `MigrateAsync` for both `"Testing"` and `"E2E"`.
3. Add `POST /api/test/reset` endpoint — gated so it only registers when env is `Testing` or `E2E`.
4. Update `IntegrationTestFactory` to read `DefaultConnection` from configuration instead of hardcoded const.
5. Add `globalSetup.ts` and `globalTeardown.ts` in Playwright calling the reset endpoint.
6. Add postgres init SQL or docker-compose override to create `mybudget_e2e` on first run.
7. Correct `openspec/config.yaml` testing note.

---

## Risks

| Risk | Severity | Notes |
|---|---|---|
| Reset endpoint exposed in production | HIGH | Must be gated at registration time, not authorization |
| `CleanDatabaseAsync()` N DELETEs will degrade at scale | LOW | Acceptable now; document for future Respawn migration |
| Playwright API URL implicitly proxied through Vite | MEDIUM | Any proxy target change silently breaks E2E |
| `appsettings.Testing.json` exists but factory ignores it | LOW | Safe to switch; file already ships to test output dir |
| Docker init SQL must run before migrations | MEDIUM | Requires postgres initialization hooks — fiddly with docker-compose |

---

## Ready for Proposal

Yes. Scope is bounded, pattern is established, no architectural blockers.
