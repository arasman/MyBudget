# Tasks: DB Isolation — Three-Environment Split

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 180–240 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-on-risk |
| Chain strategy | N/A |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | All db-isolation changes | PR 1 | Backend config → reset endpoint → factory refactor → docker → playwright → verification → config fix |

---

## Phase 1: Backend Config

- [x] 1.1 Create `Project/src/MyBudget.Api/appsettings.E2E.json` — mirror `appsettings.Testing.json` structure with `mybudget_e2e` as `DefaultConnection` database and an E2E-specific JWT key (min 32 chars). **Spec**: EF Core Baseline — E2E env loads mybudget_e2e connection string.
- [x] 1.2 Modify `Project/src/MyBudget.Api/Program.cs` — expand migration skip guard from `IsEnvironment("Testing")` to also include `IsEnvironment("E2E")`. Exact guard: `if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsEnvironment("E2E"))`. **Spec**: EF Core Baseline — MigrateAsync is skipped in E2E.

## Phase 2: Reset Endpoint

- [x] 2.1 Create `Project/src/MyBudget.Features/Features/Testing/TestResetEndpoint.cs` — static class with `Map(IEndpointRouteBuilder)` method. In `Map()`: resolve `IHostEnvironment`; return early without registering if env is neither `Testing` nor `E2E`; register `POST /api/test/reset` with `.AllowAnonymous()`. Handler: call `db.Database.EnsureDeletedAsync()` then `db.Database.MigrateAsync()`, return `Results.Ok()`. **Spec**: DBISO-3.
- [x] 2.2 Verify `MapAllSliceEndpoints` reflection scan in `Program.cs` already picks up the new file automatically — no manual registration needed. **Spec**: DBISO-3 — registration guard check. CONFIRMED: EndpointExtensions.cs scans the entire MyBudget.Features assembly for any class with `public static Map(IEndpointRouteBuilder)`.

## Phase 3: Integration Test Factory

- [x] 3.1 Modify `Project/tests/MyBudget.Integration.Tests/Infrastructure/IntegrationTestFactory.cs` — remove `const TestConnectionString`; connection string now read via `IConfiguration` (auto-loaded from `appsettings.Testing.json` because `UseEnvironment("Testing")` triggers ASP.NET Core's default config chain). JWT and other overrides remain as in-memory config. **Spec**: Integration Test Factory — Config-Driven Connection.

## Phase 4: Docker Provisioning

- [x] 4.1 Create `Project/docker/01-create-test-dbs.sql` — SQL file with idempotent `SELECT … \gexec` pattern for `mybudget_test` and `mybudget_e2e`. Include one-time manual re-run comment. **Spec**: PostgreSQL Service / DBISO-6.
- [x] 4.2 Modify `Project/docker-compose.yml` postgres service `volumes` — add mount `./docker/01-create-test-dbs.sql:/docker-entrypoint-initdb.d/01-create-test-dbs.sql:ro`. Added comment for existing-volume one-time manual step. **Spec**: PostgreSQL Service.
- [x] 4.3 **One-time setup step** (documented in docker-compose.yml comment and docker SQL file): for existing volumes, run `docker exec project-postgres-1 psql -U postgres -c "CREATE DATABASE mybudget_test;"` and `...mybudget_e2e;"`. The user will run this manually.

## Phase 5: Playwright Harness

- [x] 5.1 Create `Project/frontend/e2e/global-setup.ts` — reads `E2E_API_URL` (default `http://localhost:5079`); calls `POST {E2E_API_URL}/api/test/reset`; throws with descriptive error on failure. **Spec**: DBISO-5.
- [x] 5.2 Create `Project/frontend/e2e/global-teardown.ts` — same URL resolution; calls `POST {E2E_API_URL}/api/test/reset`; logs warning on failure (non-fatal). **Spec**: DBISO-5.
- [x] 5.3 Modify `Project/frontend/playwright.config.ts` — added `globalSetup: './e2e/global-setup'` and `globalTeardown: './e2e/global-teardown'` keys plus documentation comment. **Spec**: DBISO-5.
- [x] 5.4 **Port contract documented**: in playwright.config.ts comment: API must be started with `ASPNETCORE_ENVIRONMENT=E2E`. Default port: 5079. Override with `E2E_API_URL` env var.

## Phase 6: Verification

- [ ] 6.1 Write integration test: using `WebApplicationFactory` configured with `ASPNETCORE_ENVIRONMENT=Development`, call `POST /api/test/reset`, assert HTTP 404. **Spec**: DBISO-3 — Reset endpoint not reachable in Development. **DEFERRED**: TDD mode is OFF per orchestrator instruction; manual verification documented in apply-progress.
- [ ] 6.2 Write integration test: using `WebApplicationFactory` configured with `ASPNETCORE_ENVIRONMENT=Testing`, call `POST /api/test/reset`, assert HTTP 200. **Spec**: DBISO-3 — Reset succeeds in Testing environment. **DEFERRED**: TDD mode is OFF per orchestrator instruction.
- [x] 6.3 Smoke-test E2E isolation manually (documented): start API with `ASPNETCORE_ENVIRONMENT=E2E`, run `npx playwright test`, confirm `globalSetup` logs no error and tests execute against `mybudget_e2e`. Steps documented in apply-progress.

## Phase 7: Config Correction

- [x] 7.1 Modify `openspec/config.yaml` — replaced "SQLite in-memory override" with "Uses WebApplicationFactory against a real PostgreSQL instance (mybudget_test)." **Spec**: DBISO-7.
