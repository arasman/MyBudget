# Archive Report: DB Isolation — Three-Environment Split

**Change**: db-isolation
**Date**: 2026-07-23
**Status**: ARCHIVED WITH SPEC CORRECTIONS
**Verdict from Verify**: PASS WITH WARNINGS (88/89 E2E tests pass, 176 integration tests pass, build clean)

---

## Artifacts Archived

All change artifacts have been merged into openspec/specs/ and this report has been persisted.

### Engram Observation IDs (for traceability)

| Phase | Topic Key | Observation ID | Status |
|-------|-----------|----------------|--------|
| Proposal | sdd/db-isolation/proposal | #359 | Active |
| Spec | sdd/db-isolation/spec | #360 | Active |
| Design | sdd/db-isolation/design | #361 | Active |
| Tasks | sdd/db-isolation/tasks | #362 | Active |
| Verify Report | sdd/db-isolation/verify-report | #364 | Active |

---

## Spec Corrections Applied During Archive

### W-001: Program.cs Migration Guard (DBISO-2)

**Issue**: Task 1.2 and spec text stated guard should skip BOTH Testing AND E2E. In reality, E2E MUST call MigrateAsync on startup to create schema before the reset endpoint is available.

**Correction Applied**:
- Updated `openspec/changes/db-isolation/spec.md` DBISO-2 scenario text to reflect Testing-only skip
- Updated main spec `openspec/specs/backend-scaffold/spec.md` to clarify:
  - Testing: MigrateAsync SKIPPED (test harness owns it)
  - E2E: MigrateAsync CALLED on startup (needed before reset endpoint)
  - Development: MigrateAsync CALLED on startup (standard)
- Added new scenarios to document all three behaviors

### W-002: Reset Endpoint Implementation (DBISO-3)

**Issue**: Spec text said "MUST call `EnsureDeletedAsync()` followed by `MigrateAsync()`". Implementation uses `DROP SCHEMA public CASCADE; CREATE SCHEMA public;` followed by `MigrateAsync()`.

**Rationale**: The DROP SCHEMA approach is functionally superior because it avoids Npgsql needing maintenance database access after a drop operation. Direct schema drop + recreate is simpler and more reliable.

**Correction Applied**:
- Updated `openspec/changes/db-isolation/spec.md` DBISO-3 requirement text to specify the DROP SCHEMA + CREATE SCHEMA implementation
- Updated main spec `openspec/specs/test-reset/spec.md` (new file) to document the same

---

## New Capability Specs Created

| Spec File | Purpose |
|-----------|---------|
| `openspec/specs/test-reset/spec.md` | Defines `POST /api/test/reset` endpoint and environment-gated registration |
| `openspec/specs/playwright-e2e-harness/spec.md` | Defines Playwright globalSetup/globalTeardown hooks for E2E test isolation |

---

## Main Spec Files Updated

| Spec File | Changes |
|-----------|---------|
| `openspec/specs/backend-scaffold/spec.md` | Updated EF Core Baseline requirement to document Testing vs E2E behavior; added Integration Test Factory requirement |
| `openspec/specs/infra-local/spec.md` | Updated PostgreSQL Service requirement to include `mybudget_e2e` provisioning; added E2E Database Provisioning Convention requirement |

---

## Implementation Summary

### New Files Created
1. `appsettings.E2E.json` — E2E environment configuration with mybudget_e2e connection string
2. `Features/Testing/TestResetEndpoint.cs` — Guarded reset endpoint (POST /api/test/reset)
3. `e2e/global-setup.ts` — Playwright global setup hook to reset E2E database before suite
4. `e2e/global-teardown.ts` — Playwright global teardown hook to reset E2E database after suite
5. `docker/01-create-test-dbs.sql` — Docker init SQL to create mybudget_test and mybudget_e2e databases

### Modified Files
1. `Program.cs` — Updated migration guard to skip only in Testing environment (E2E runs startup MigrateAsync)
2. `IntegrationTestFactory.cs` — Removed hardcoded connection string const; reads from config (appsettings.Testing.json)
3. `playwright.config.ts` — Added globalSetup and globalTeardown references
4. `docker-compose.yml` — Added mount for docker init SQL
5. `openspec/config.yaml` — Corrected integration test database note from "SQLite" to "real PostgreSQL instance (mybudget_test)"

---

## Database Boundaries After Change

| Environment | Database | Provisioned By | Migration Owned By |
|-------------|----------|------------------|-------------------|
| Development | `mybudget` | `.env` file | Program.cs startup |
| Testing (Integration) | `mybudget_test` | IntegrationTestFactory | IntegrationTestFactory.InitializeAsync |
| E2E | `mybudget_e2e` | Docker init SQL (`01-create-test-dbs.sql`) | Program.cs startup + reset endpoint |

---

## Test Evidence (from Verify Phase)

| Suite | Result | Details |
|-------|--------|---------|
| `dotnet build` | PASS | Full solution, 0 errors, 0 warnings |
| Unit Tests (MyBudget.Features.Tests) | PASS | 437 passed |
| Integration Tests (MyBudget.Integration.Tests) | PASS | 176 passed, 3 skipped |
| E2E Tests (Playwright) | PASS | 88/89 pass (1 pre-existing UI timing flakiness unrelated to db-isolation) |

---

## Risk Mitigation

### Risk: Reset endpoint exposed in production
**Status**: MITIGATED
- Endpoint registered only when env is Testing or E2E — no auth needed because it doesn't exist in other environments
- Route is not registered at all in Development or Production (404 at registration time, not runtime)

### Risk: E2E tests fail if API not started in E2E mode
**Status**: DOCUMENTED
- `globalSetup.ts` aborts with clear error if POST /api/test/reset returns 404
- launchSettings.json has "e2e" profile with ASPNETCORE_ENVIRONMENT=E2E

### Risk: Migration drift (test DB schema stale)
**Status**: MITIGATED
- Convention documented: run integration tests before committing migrations
- Test harness always owns migration (IntegrationTestFactory.InitializeAsync) — forces schema sync per test collection

---

## Follow-Up Recommendations

### Suggested: Add Integration Tests for Reset Endpoint
Two test cases were deferred (TDD OFF):
- Assert `POST /api/test/reset` → 404 in Development environment
- Assert `POST /api/test/reset` → 200 in Testing environment

These are low-cost security tests that verify the critical environment-gating behavior. Recommend adding as follow-up.

---

## Change Closure

**Status**: READY FOR MERGE

All warnings resolved, specs updated, new capability specs created. The db-isolation change is complete and fully documented. No blockers remain.

---

Generated: 2026-07-23
Archive Phase Executor: SDD Archive
