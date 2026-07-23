# Apply Progress: DB Isolation — Three-Environment Split

**Change**: db-isolation
**Mode**: Standard (TDD OFF)
**Date**: 2026-07-23

---

## Completed Tasks

- [x] 1.1 Created `appsettings.E2E.json` with `mybudget_e2e` connection string and E2E JWT key
- [x] 1.2 Expanded migration skip guard in `Program.cs` to cover both `Testing` and `E2E`
- [x] 2.1 Created `TestResetEndpoint.cs` in `Features/Testing/` — env-gated, auto-discovered by reflection scan
- [x] 2.2 Verified `MapAllSliceEndpoints` covers `Testing` namespace — `EndpointExtensions.cs` scans the entire `MyBudget.Features` assembly
- [x] 3.1 Refactored `IntegrationTestFactory.cs` — removed `const TestConnectionString`; connection string now read from `IConfiguration` (auto-loaded from `appsettings.Testing.json` via `UseEnvironment("Testing")`)
- [x] 4.1 Created `Project/docker/01-create-test-dbs.sql` with idempotent postgres `\gexec` pattern for `mybudget_test` and `mybudget_e2e`
- [x] 4.2 Updated `docker-compose.yml` — mounted init SQL into postgres `docker-entrypoint-initdb.d/`; added comment for existing-volume manual step
- [x] 4.3 Documented one-time manual step in docker-compose.yml comment (user will run manually — docker volume already exists for 11 days)
- [x] 5.1 Created `Project/frontend/e2e/global-setup.ts` with `E2E_API_URL` env var support and clear error on failure
- [x] 5.2 Created `Project/frontend/e2e/global-teardown.ts` with non-fatal warning on failure
- [x] 5.3 Updated `playwright.config.ts` — added `globalSetup` and `globalTeardown` keys plus documentation comment
- [x] 5.4 Port contract documented in `playwright.config.ts` comment
- [x] 6.3 Manual smoke-test steps documented below
- [x] 7.1 Fixed `openspec/config.yaml` integration test note — removed "SQLite in-memory override" falsehood

## Deferred Tasks

- [ ] 6.1 Integration test: assert `POST /api/test/reset` → 404 in Development — deferred, TDD OFF
- [ ] 6.2 Integration test: assert `POST /api/test/reset` → 200 in Testing — deferred, TDD OFF

---

## Files Changed

| File | Action | Description |
|------|--------|-------------|
| `Project/src/MyBudget.Api/appsettings.E2E.json` | Created | E2E connection string (mybudget_e2e) + JWT key |
| `Project/src/MyBudget.Api/Program.cs` | Modified | Migration guard expanded to skip E2E as well |
| `Project/src/MyBudget.Features/Features/Testing/TestResetEndpoint.cs` | Created | `POST /api/test/reset` — env-gated, auto-discovered |
| `Project/tests/MyBudget.Integration.Tests/Infrastructure/IntegrationTestFactory.cs` | Modified | Removed hardcoded const; reads conn string from IConfiguration |
| `Project/docker/01-create-test-dbs.sql` | Created | Idempotent SQL to create mybudget_test + mybudget_e2e |
| `Project/docker-compose.yml` | Modified | Mounted init SQL into postgres service |
| `Project/frontend/e2e/global-setup.ts` | Created | Playwright globalSetup — resets E2E DB before suite |
| `Project/frontend/e2e/global-teardown.ts` | Created | Playwright globalTeardown — resets E2E DB after suite |
| `Project/frontend/playwright.config.ts` | Modified | Added globalSetup + globalTeardown + documentation |
| `openspec/config.yaml` | Modified | Corrected integration test DB note (Postgres, not SQLite) |

---

## Task 2.2 Finding — MapAllSliceEndpoints Scan

`EndpointExtensions.cs` scans `typeof(EndpointExtensions).Assembly` (i.e., the entire `MyBudget.Features` assembly) for any class with `public static Map(IEndpointRouteBuilder)`. The `Testing` namespace is inside this assembly, so `TestResetEndpoint` is auto-discovered with zero manual registration. No changes to `Program.cs` or `EndpointExtensions.cs` were needed beyond the migration guard.

---

## Task 3.1 Design Note

`appsettings.Testing.json` is auto-loaded by the ASP.NET Core host when `UseEnvironment("Testing")` is set. The `ConfigureAppConfiguration` callback runs after the host's default config sources, which means `configuration["ConnectionStrings:DefaultConnection"]` in `ConfigureServices` already resolves from `appsettings.Testing.json` without an explicit `AddJsonFile` call. The previous hardcoded `const TestConnectionString` duplicated the value from `appsettings.Testing.json`; the two were in sync, but the const was a fragile second source of truth.

---

## Docker Volume Context (Critical)

The existing postgres container (`project-postgres-1`) has a volume initialized 11 days ago. Init SQL scripts in `/docker-entrypoint-initdb.d/` only run on first `initdb`. Therefore:

- The SQL file was written for fresh setups and other developers.
- For the existing running container, the user must run manually:
  ```
  docker exec project-postgres-1 psql -U postgres -c "CREATE DATABASE mybudget_test;"
  docker exec project-postgres-1 psql -U postgres -c "CREATE DATABASE mybudget_e2e;"
  ```
- This is documented in `docker-compose.yml` comments and the SQL file header.

---

## Phase 6: Manual Verification Steps

### 6.1 — Reset endpoint not reachable in Development

1. Start API: `ASPNETCORE_ENVIRONMENT=Development dotnet run` (from `Project/src/MyBudget.Api/`)
2. `curl -X POST http://localhost:5079/api/test/reset`
3. Expected: HTTP 404 (route was never registered)

### 6.2 — Reset endpoint reachable in Testing

1. Start API: `ASPNETCORE_ENVIRONMENT=Testing dotnet run`
2. `curl -X POST http://localhost:5079/api/test/reset`
3. Expected: HTTP 200 and `mybudget_test` is wiped + re-migrated

### 6.3 — Full E2E smoke test

1. Start API: `ASPNETCORE_ENVIRONMENT=E2E dotnet run` (from `Project/src/MyBudget.Api/`)
2. Start frontend: `pnpm dev` (from `Project/frontend/`)
3. Run Playwright: `pnpm exec playwright test` (from `Project/frontend/`)
4. Verify `globalSetup` logs no error and the suite runs against `mybudget_e2e`

---

## Deviations from Design

- **Task 3.1**: Design said "add `ConfigureAppConfiguration` to load `appsettings.Testing.json`". Implementation relies on ASP.NET Core's auto-load instead of an explicit `AddJsonFile` call. The result is identical (connection string comes from `appsettings.Testing.json`) but avoids loading the file twice. This is strictly more correct.
- **Tasks 6.1/6.2**: Integration tests not written — TDD OFF per orchestrator instruction. These are candidates for a follow-up apply batch or manual verification.

---

## Workload / PR Boundary

- Mode: single PR
- Estimated review budget impact: ~180–200 lines changed (within 180–240 forecast)
- All tasks in scope are complete except 6.1/6.2 (TDD OFF deferral)
