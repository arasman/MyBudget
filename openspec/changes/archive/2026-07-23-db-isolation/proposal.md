# Proposal: DB Isolation — Three-Environment Split

## Intent

E2E tests currently hit the dev database (`mybudget`), causing unbounded data accumulation and cross-contamination. Integration tests are isolated but the factory hardcodes the connection string instead of reading config. This change enforces a strict DB-per-environment boundary: `mybudget` (dev), `mybudget_test` (integration), `mybudget_e2e` (E2E), so no test run can corrupt developer data.

## Scope

### In Scope
- `appsettings.E2E.json` with `mybudget_e2e` connection string
- `Program.cs`: skip `MigrateAsync` for both `Testing` and `E2E` environments
- `POST /api/test/reset` endpoint — registered only when env is `Testing` or `E2E` (security gate at registration, not middleware)
- `IntegrationTestFactory`: read `DefaultConnection` from `appsettings.Testing.json` instead of hardcoded const
- Playwright `globalSetup.ts` + `globalTeardown.ts` calling `/api/test/reset`
- Docker-compose or postgres init SQL to create `mybudget_e2e` database
- Fix `openspec/config.yaml` integration test note (says SQLite — actually Postgres)
- Document migration convention: run integration tests before committing new migrations

### Out of Scope
- Respawn library adoption (future optimization)
- CI pipeline changes (no GitHub Actions files exist)
- New test data seeding beyond current patterns
- Unit test changes (SQLite in-memory, unaffected)

## Capabilities

### New Capabilities
- `test-reset`: `POST /api/test/reset` endpoint — environment-gated DB wipe for test harnesses

### Modified Capabilities
- `infra-local`: docker-compose gains `mybudget_e2e` DB creation (init SQL or entrypoint)
- `backend-scaffold`: `Program.cs` environment check expands; `IntegrationTestFactory` reads config

## Approach

**Approach B — Standard three-environment split** (recommended by exploration).

1. Add `appsettings.E2E.json` with `mybudget_e2e` connection string + JWT key
2. Expand `Program.cs` migration skip: `IsEnvironment("Testing") || IsEnvironment("E2E")`
3. Add `TestResetEndpoint` mapped only when `IHostEnvironment` is `Testing` or `E2E` — calls `EnsureDeletedAsync()` + `MigrateAsync()` on `AppDbContext`
4. Refactor `IntegrationTestFactory` to read `DefaultConnection` from config chain instead of const
5. Add `globalSetup.ts`: start API in E2E mode, call `/api/test/reset`, seed if needed
6. Add `globalTeardown.ts`: call reset or no-op
7. Add postgres init SQL (`docker-entrypoint-initdb.d/`) to `CREATE DATABASE mybudget_e2e`
8. Fix `openspec/config.yaml` line 33 note

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Project/src/MyBudget.Api/appsettings.E2E.json` | New | E2E connection string + JWT config |
| `Project/src/MyBudget.Api/Program.cs` | Modified | Skip migration for E2E; register test-reset endpoint |
| `Project/tests/MyBudget.Integration.Tests/Infrastructure/IntegrationTestFactory.cs` | Modified | Read conn string from config instead of const |
| `Project/frontend/playwright.config.ts` | Modified | Add globalSetup/globalTeardown references |
| `Project/frontend/e2e/global-setup.ts` | New | Call /api/test/reset before E2E suite |
| `Project/frontend/e2e/global-teardown.ts` | New | Cleanup after E2E suite |
| `Project/docker-compose.yml` or init SQL | Modified | Create mybudget_e2e DB |
| `openspec/config.yaml` | Modified | Fix incorrect SQLite note |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Reset endpoint exposed in production | Low | Endpoint registered only when env is Testing/E2E — not auth-gated, not registered at all in other envs |
| E2E tests fail if API not started in E2E mode | Med | Document required startup; consider playwright `webServer` block |
| Migration drift (test DB schema stale) | Low | Convention: run integration tests before committing migrations; test harness always runs MigrateAsync |

## Rollback Plan

Revert the commit. No data migrations, no schema changes to dev DB, no new dependencies. The reset endpoint only exists in test environments so removing it has zero production impact.

## Dependencies

- Docker Desktop running with postgres container (existing requirement)
- No new NuGet or npm packages

## Success Criteria

- [x] `dotnet test tests/MyBudget.Integration.Tests/` passes reading conn string from config
- [x] E2E tests run against `mybudget_e2e`, verified by checking DB name in test output
- [x] Dev database (`mybudget`) has zero test-generated rows after full test suite run
- [x] `POST /api/test/reset` returns 404 when API runs in Development mode
- [x] `POST /api/test/reset` returns 200 and wipes+migrates when API runs in E2E mode
- [x] `openspec/config.yaml` integration note corrected to mention Postgres

## Migration Management Convention

When adding a new EF Core migration, run `dotnet test tests/MyBudget.Integration.Tests/` before committing. The test harness calls `EnsureDeletedAsync()` + `MigrateAsync()`, so any migration error surfaces immediately. Dev DB auto-migrates on startup as before.
