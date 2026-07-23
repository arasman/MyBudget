# Delta Spec: DB Isolation — Three-Environment Split

## Capabilities Covered

| Capability | Type | Domains |
|---|---|---|
| `test-reset` | New | Guarded reset endpoint |
| `backend-scaffold` | Modified | EF Core Baseline, Program.cs Pipeline, Integration Test Factory |
| `infra-local` | Modified | PostgreSQL Service |

---

## New Capability: test-reset

### Requirement: DBISO-3 — Environment-Gated Reset Endpoint

`POST /api/test/reset` MUST exist and MUST be registered in `Program.cs` only when `ASPNETCORE_ENVIRONMENT` is `Testing` or `E2E`. The check MUST occur at registration time, not at request time. The endpoint MUST call `EnsureDeletedAsync()` followed by `MigrateAsync()` on `AppDbContext` and return HTTP 200 on success. The endpoint MUST NOT be reachable in any environment that is not `Testing` or `E2E`.

#### Scenario: Reset succeeds in E2E environment

- GIVEN the API is running with `ASPNETCORE_ENVIRONMENT=E2E`
- WHEN `POST /api/test/reset` is called
- THEN the database is wiped and re-migrated and the endpoint returns HTTP 200

#### Scenario: Reset succeeds in Testing environment

- GIVEN the API is running with `ASPNETCORE_ENVIRONMENT=Testing`
- WHEN `POST /api/test/reset` is called
- THEN the database is wiped and re-migrated and the endpoint returns HTTP 200

#### Scenario: Reset endpoint is not reachable in Development

- GIVEN the API is running with `ASPNETCORE_ENVIRONMENT=Development`
- WHEN `POST /api/test/reset` is called
- THEN the endpoint returns HTTP 404 (route was never registered)

#### Scenario: Reset endpoint is not reachable in Production

- GIVEN the API is running with `ASPNETCORE_ENVIRONMENT=Production`
- WHEN `POST /api/test/reset` is called
- THEN the endpoint returns HTTP 404 (route was never registered)

---

## Delta for backend-scaffold

### MODIFIED Requirements

#### Requirement: EF Core Baseline

`AppDbContext` MUST use the Npgsql EF Core provider. A single empty migration named `InitialCreate` MUST exist under `Project/src/MyBudget.Features/Migrations/`. The application MUST call `MigrateAsync()` on startup before handling any HTTP request, EXCEPT when `ASPNETCORE_ENVIRONMENT` is `Testing` or `E2E` — in those environments `MigrateAsync()` MUST NOT be called by `Program.cs`. All `decimal` columns MUST have global precision `(18, 2)` configured in `OnModelCreating`. No secrets (connection strings) MAY appear in `appsettings.json`. Each environment MUST load its database connection string exclusively from its environment-specific appsettings layer: `appsettings.Testing.json` MUST target `mybudget_test`; `appsettings.E2E.json` MUST target `mybudget_e2e`; Development MUST NOT load either test connection string.

(Previously: `MigrateAsync()` was called unconditionally on startup; only `Testing` was excluded; no E2E config file existed.)

##### Scenario: Migration creates the history table on a clean database

- GIVEN a PostgreSQL instance is running and the database does not exist
- WHEN the application starts in Development and `MigrateAsync()` is called
- THEN the `__EFMigrationsHistory` table is created and contains one row for `InitialCreate`

##### Scenario: MigrateAsync is skipped when environment is Testing

- GIVEN the API starts with `ASPNETCORE_ENVIRONMENT=Testing`
- WHEN the startup pipeline completes
- THEN `MigrateAsync()` is NOT called by `Program.cs` (test harness owns migration)

##### Scenario: MigrateAsync is skipped when environment is E2E

- GIVEN the API starts with `ASPNETCORE_ENVIRONMENT=E2E`
- WHEN the startup pipeline completes
- THEN `MigrateAsync()` is NOT called by `Program.cs` (E2E harness owns migration via reset endpoint)

##### Scenario: E2E environment loads mybudget_e2e connection string

- GIVEN `appsettings.E2E.json` exists with `DefaultConnection` pointing to `mybudget_e2e`
- WHEN the API starts with `ASPNETCORE_ENVIRONMENT=E2E`
- THEN `AppDbContext` and `ConnectionFactory` both resolve to `mybudget_e2e`

##### Scenario: Testing environment loads mybudget_test connection string

- GIVEN `appsettings.Testing.json` exists with `DefaultConnection` pointing to `mybudget_test`
- WHEN the API starts with `ASPNETCORE_ENVIRONMENT=Testing`
- THEN `AppDbContext` resolves to `mybudget_test`

##### Scenario: Decimal precision is enforced globally

- GIVEN an entity with a `decimal` property is added to the model
- WHEN the EF Core model is built
- THEN the column has precision 18 and scale 2 without requiring per-property configuration

---

#### Requirement: Integration Test Factory — Config-Driven Connection

`IntegrationTestFactory` MUST read `DefaultConnection` from `IConfiguration` (resolved from `appsettings.Testing.json`) and MUST NOT use a hardcoded connection string constant. The factory MUST call `EnsureDeletedAsync()` + `MigrateAsync()` per test collection to own schema state for `mybudget_test`.

(Previously: `IntegrationTestFactory` used a hardcoded `const` string for the connection and did not read any config file.)

##### Scenario: Factory reads connection string from config

- GIVEN `appsettings.Testing.json` contains `DefaultConnection` pointing to `mybudget_test`
- WHEN `IntegrationTestFactory` initializes
- THEN it connects to `mybudget_test` without any hardcoded constant

##### Scenario: Factory wipes and migrates per collection

- GIVEN a new test collection starts
- WHEN `InitializeAsync` runs
- THEN `EnsureDeletedAsync()` is called followed by `MigrateAsync()` and the schema is current

---

### ADDED Requirements

#### Requirement: DBISO-5 — Playwright E2E Harness

`globalSetup.ts` MUST call `POST /api/test/reset` before any E2E test runs. `globalTeardown.ts` SHOULD call `POST /api/test/reset` after all E2E tests complete. `playwright.config.ts` MUST register both `globalSetup` and `globalTeardown`. The API MUST be started with `ASPNETCORE_ENVIRONMENT=E2E` for any E2E run.

##### Scenario: Global setup resets the E2E database before suite

- GIVEN the API is running with `ASPNETCORE_ENVIRONMENT=E2E`
- WHEN `globalSetup.ts` executes before any Playwright test
- THEN `POST /api/test/reset` returns HTTP 200 and the E2E database is clean

##### Scenario: Global teardown resets after suite

- GIVEN all E2E tests have completed
- WHEN `globalTeardown.ts` executes
- THEN `POST /api/test/reset` is called and the E2E database is left in a clean state

##### Scenario: E2E run with wrong environment fails at setup

- GIVEN the API is running with `ASPNETCORE_ENVIRONMENT=Development`
- WHEN `globalSetup.ts` calls `POST /api/test/reset`
- THEN it receives HTTP 404 and SHOULD abort the test suite with a clear error

---

## Delta for infra-local

### MODIFIED Requirements

#### Requirement: PostgreSQL Service

The postgres service MUST use the `postgres:16-alpine` image. It MUST expose port `5432` on the host. Data MUST be persisted in a named volume `postgres-data`. The database name, user, and password MUST NOT be hardcoded in `docker-compose.yml` — they MUST be read from a `.env` file. The postgres service MUST additionally create the `mybudget_e2e` database on first container initialization, either via an init SQL file in `docker-entrypoint-initdb.d/` or an equivalent entrypoint mechanism. `mybudget_e2e` MUST exist and be accessible before any E2E run.

(Previously: only the primary database defined in `.env` was created; `mybudget_e2e` did not exist.)

##### Scenario: PostgreSQL persists data across restarts

- GIVEN postgres is running and a table has been created in the database
- WHEN `docker compose --profile infra down` and `docker compose --profile infra up -d` are run in sequence
- THEN the previously created table still exists (volume was not deleted)

##### Scenario: PostgreSQL credentials come from .env

- GIVEN the `.env` file contains `POSTGRES_USER`, `POSTGRES_PASSWORD`, and `POSTGRES_DB` values
- WHEN postgres starts
- THEN the database is accessible using those credentials and no credentials appear in `docker-compose.yml`

##### Scenario: mybudget_e2e database exists after first container start

- GIVEN no postgres volume exists (first run)
- WHEN `docker compose --profile infra up -d` is executed
- THEN `mybudget_e2e` database exists and is connectable before any E2E test runs

##### Scenario: mybudget_e2e persists across restarts

- GIVEN postgres is running with `mybudget_e2e` present and containing migrated schema
- WHEN the container is restarted
- THEN `mybudget_e2e` is still accessible (volume preserved)

---

## ADDED Requirements (infra-local)

### Requirement: DBISO-6 — E2E Database Provisioning Convention

`mybudget_e2e` MUST be documented as automatically provisioned via docker init SQL. `mybudget_test` provisioning MUST be documented as owned by `IntegrationTestFactory` via `EnsureDeletedAsync()` — no manual creation is required. `mybudget` (dev) MUST be provisioned via `.env` as before.

#### Scenario: Developer onboarding creates all three databases

- GIVEN a developer has Docker Desktop running and has run `docker compose --profile infra up -d`
- WHEN they inspect the postgres instance
- THEN `mybudget_e2e` exists (created by init SQL) and `mybudget` exists (from `.env`); `mybudget_test` is created on first integration test run

---

## MODIFIED Requirements (global — config.yaml correction)

### Requirement: DBISO-7 — openspec/config.yaml Accuracy

`openspec/config.yaml` MUST NOT state that integration tests use SQLite in-memory. The integration test note MUST accurately reflect that `MyBudget.Integration.Tests` runs against a real PostgreSQL instance (`mybudget_test`).

(Previously: config.yaml documented integration tests as "SQLite in-memory override" — factually incorrect.)

#### Scenario: Config note reflects real Postgres usage

- GIVEN `openspec/config.yaml` has been corrected
- WHEN a developer reads the integration test documentation note
- THEN it states PostgreSQL (`mybudget_test`) as the integration test database, not SQLite
