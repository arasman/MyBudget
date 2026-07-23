# playwright-e2e-harness Specification

## Purpose

Define the Playwright E2E test harness configuration, including globalSetup and globalTeardown hooks for test database isolation, and environment configuration for E2E runs.

## Requirements

### Requirement: Playwright E2E Harness

`globalSetup.ts` MUST call `POST /api/test/reset` before any E2E test runs. `globalTeardown.ts` SHOULD call `POST /api/test/reset` after all E2E tests complete. `playwright.config.ts` MUST register both `globalSetup` and `globalTeardown`. The API MUST be started with `ASPNETCORE_ENVIRONMENT=E2E` for any E2E run.

#### Scenario: Global setup resets the E2E database before suite

- GIVEN the API is running with `ASPNETCORE_ENVIRONMENT=E2E`
- WHEN `globalSetup.ts` executes before any Playwright test
- THEN `POST /api/test/reset` returns HTTP 200 and the E2E database is clean

#### Scenario: Global teardown resets after suite

- GIVEN all E2E tests have completed
- WHEN `globalTeardown.ts` executes
- THEN `POST /api/test/reset` is called and the E2E database is left in a clean state

#### Scenario: E2E run with wrong environment fails at setup

- GIVEN the API is running with `ASPNETCORE_ENVIRONMENT=Development`
- WHEN `globalSetup.ts` calls `POST /api/test/reset`
- THEN it receives HTTP 404 and SHOULD abort the test suite with a clear error
