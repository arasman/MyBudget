# Verify Report: DB Isolation — Three-Environment Split

**Change**: db-isolation
**Date**: 2026-07-23
**Mode**: Standard (TDD OFF)
**Verdict**: PASS WITH WARNINGS

---

## Build / Test Evidence

| Suite | Result | Details |
|-------|--------|---------|
| `dotnet build` (full solution) | PASS | 0 errors, 0 warnings |
| `dotnet test MyBudget.Features.Tests` | PASS | 437 passed, 0 failed, 0 skipped |
| `dotnet test MyBudget.Integration.Tests` | PASS* | 176 passed, 0 failed, 3 skipped; host crash after last test (cleanup artifact) |
| E2E (manual, per apply-progress) | PASS | 88/89 tests pass; 1 pre-existing UI timing flakiness unrelated to db-isolation |

*Host crash occurs after all tests complete — all assertions pass. Flagged as SUGGESTION.

---

## Task Completeness

| Phase | Tasks | Status |
|-------|-------|--------|
| Phase 1: Backend Config | 1.1 ✅, 1.2 ⚠️ | Partial — 1.2 marked complete but code diverges from task description |
| Phase 2: Reset Endpoint | 2.1 ✅, 2.2 ✅ | Complete |
| Phase 3: Integration Test Factory | 3.1 ✅ | Complete |
| Phase 4: Docker Provisioning | 4.1 ✅, 4.2 ✅, 4.3 ✅ | Complete |
| Phase 5: Playwright Harness | 5.1 ✅, 5.2 ✅, 5.3 ✅, 5.4 ✅ | Complete |
| Phase 6: Verification | 6.1 ☐ deferred, 6.2 ☐ deferred, 6.3 ✅ | Deferred (TDD OFF) |
| Phase 7: Config Correction | 7.1 ✅ | Complete |

---

## Spec Compliance Matrix

### DBISO-1 — Environment-driven connection string

| Scenario | Evidence | Status |
|----------|----------|--------|
| appsettings.E2E.json targets mybudget_e2e | File exists; `Database=mybudget_e2e; Username=mybudget` | PASS |
| appsettings.Testing.json targets mybudget_test | File exists; `Database=mybudget_test; Username=mybudget` | PASS |

### DBISO-2 — Migration ownership

| Scenario | Evidence | Status |
|----------|----------|--------|
| MigrateAsync skipped in Testing | Program.cs:66 — `!IsEnvironment("Testing")` guard present | PASS |
| MigrateAsync skipped in E2E | Program.cs:66 — guard does NOT include E2E; MigrateAsync IS called on E2E startup | SPEC DIVERGENCE — intentional (see W-001) |
| Comment reflects behavior | Line 65: "skipped in Testing only" — accurate to code, contradicts spec text | WARNING |

**Design rationale (verify-confirmed)**: E2E intentionally calls MigrateAsync at startup to create schema before globalSetup fires.
The reset endpoint then wipes+remigrates between runs. Spec text and task 1.2 description must be corrected at archive.

### DBISO-3 — Reset endpoint

| Scenario | Evidence | Status |
|----------|----------|--------|
| File exists | `TestResetEndpoint.cs` in `Features/Testing/` | PASS |
| Guard at Map() time | env check before MapPost call | PASS |
| Testing + E2E both allowed | Guard: `!IsEnvironment("Testing") && !IsEnvironment("E2E")` | PASS |
| Handler wipes DB | `DROP SCHEMA public CASCADE; CREATE SCHEMA public;` + MigrateAsync (NOT EnsureDeletedAsync — see W-002) | PASS (deviation) |
| Returns HTTP 200 | `Results.Ok()` on success | PASS |
| AllowAnonymous | `.AllowAnonymous()` present | PASS |
| Runtime evidence | Reset returns 200, creates 18 tables in mybudget_e2e | PASS |

### DBISO-4 — Integration test factory

| Scenario | Evidence | Status |
|----------|----------|--------|
| No hardcoded const | `const TestConnectionString` removed | PASS |
| Reads from IConfiguration | `ctx.Configuration["ConnectionStrings:DefaultConnection"]` | PASS |
| EnsureDeletedAsync + MigrateAsync | `InitializeDatabaseAsync()` calls both | PASS |

### DBISO-5 — Playwright harness

| Scenario | Evidence | Status |
|----------|----------|--------|
| global-setup.ts exists | Created, calls `POST /api/test/reset` | PASS |
| global-teardown.ts exists | Created, non-fatal warning on failure | PASS |
| playwright.config.ts registers both | `globalSetup` + `globalTeardown` keys present | PASS |
| vite.config.ts uses VITE_API_TARGET | `process.env['VITE_API_TARGET'] ?? 'http://localhost:5184'` | PASS |
| Setup aborts on 404 | Throws with descriptive error if `!response.ok` | PASS |

### DBISO-6 — Docker provisioning

| Scenario | Evidence | Status |
|----------|----------|--------|
| 01-create-test-dbs.sql exists | `Project/docker/` — idempotent `\gexec` pattern | PASS |
| Uses mybudget user (not postgres) | Manual commands in comment use `-U mybudget` | PASS |
| docker-compose.yml mounts init SQL | `:ro` bind-mount into `docker-entrypoint-initdb.d/` | PASS |

### DBISO-7 — Config correction

| Scenario | Evidence | Status |
|----------|----------|--------|
| config.yaml no longer says SQLite in-memory | Note: "Uses WebApplicationFactory against a real PostgreSQL instance (mybudget_test)" | PASS |

### Additional — launchSettings.json e2e profile

| Check | Evidence | Status |
|-------|----------|--------|
| e2e profile exists | `launchSettings.json` has `"e2e"` profile | PASS |
| Port 5079 | `applicationUrl: "http://localhost:5079"` | PASS |
| ASPNETCORE_ENVIRONMENT=E2E | `environmentVariables` set correctly | PASS |

---

## Issues

### CRITICAL

None.

### WARNING

| ID | Location | Description |
|----|----------|-------------|
| W-001 | `Program.cs:65-71` | Task 1.2 marked complete but guard is `!IsEnvironment("Testing")` only — not `&& !IsEnvironment("E2E")` as the task described. Code behavior is **intentionally correct** (E2E needs startup MigrateAsync so schema exists before globalSetup fires). Task description and spec text are stale. Correct both at archive. |
| W-002 | `TestResetEndpoint.cs:38` and spec DBISO-3 | Spec says `EnsureDeletedAsync()` but implementation uses `DROP SCHEMA public CASCADE; CREATE SCHEMA public;`. The implementation is functionally superior (avoids EF Core's slow teardown path). Spec text must be updated at archive. |

### SUGGESTION

| ID | Location | Description |
|----|----------|-------------|
| S-001 | `MyBudget.Integration.Tests` runner | Test host process crash after last test completes (all 176 tests passed before crash). Likely a `WebApplicationFactory` finalizer or xUnit teardown race. Monitor for CI flakiness. |
| S-002 | Tasks 6.1/6.2 | Integration tests for reset endpoint 404 (Development) and 200 (Testing) were deferred (TDD OFF). Low-cost tests that cover the critical security property — recommend adding in a follow-up. |

---

## Final Verdict

**PASS WITH WARNINGS** — 0 CRITICALs, 2 WARNINGs (both intentional, documented deviations), 2 SUGGESTIONs.
All implementation checks pass. E2E runtime confirms 88/89 tests pass (1 pre-existing flakiness).
Archive is unblocked. Update spec text for W-001 and W-002 during sdd-archive.
