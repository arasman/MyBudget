# infra-local Specification

## Purpose

Define the Docker Compose infrastructure configuration for local development and CI-like environments. Provides PostgreSQL, Redis, Mailpit, Seq, and Jaeger as services controlled by profiles.

## Requirements

### Requirement: Docker Compose Profiles

`Project/docker-compose.yml` MUST define two profiles: `infra` and `full`. The `infra` profile MUST include all five infrastructure services (postgres, redis, mailpit, seq, jaeger) for local development. The `full` profile MUST include the same five services plus the backend and frontend applications for CI/prod-like runs. No service MAY start by default (without a profile flag).

#### Scenario: infra profile starts all five services

- GIVEN Docker Desktop is running and no containers are active
- WHEN `docker compose --profile infra up -d` is executed
- THEN all five services (postgres, redis, mailpit, seq, jaeger) start and report healthy

#### Scenario: No services start without a profile flag

- GIVEN Docker Desktop is running
- WHEN `docker compose up -d` is executed without a `--profile` flag
- THEN no containers are started

---

### Requirement: PostgreSQL Service

The postgres service MUST use the `postgres:16-alpine` image. It MUST expose port `5432` on the host. Data MUST be persisted in a named volume `postgres-data`. The database name, user, and password MUST NOT be hardcoded in `docker-compose.yml` — they MUST be read from a `.env` file. The postgres service MUST additionally create the `mybudget_e2e` database on first container initialization, either via an init SQL file in `docker-entrypoint-initdb.d/` or an equivalent entrypoint mechanism. `mybudget_e2e` MUST exist and be accessible before any E2E run.

#### Scenario: PostgreSQL persists data across restarts

- GIVEN postgres is running and a table has been created in the database
- WHEN `docker compose --profile infra down` and `docker compose --profile infra up -d` are run in sequence
- THEN the previously created table still exists (volume was not deleted)

#### Scenario: PostgreSQL credentials come from .env

- GIVEN the `.env` file contains `POSTGRES_USER`, `POSTGRES_PASSWORD`, and `POSTGRES_DB` values
- WHEN postgres starts
- THEN the database is accessible using those credentials and no credentials appear in `docker-compose.yml`

#### Scenario: mybudget_e2e database exists after first container start

- GIVEN no postgres volume exists (first run)
- WHEN `docker compose --profile infra up -d` is executed
- THEN `mybudget_e2e` database exists and is connectable before any E2E test runs

#### Scenario: mybudget_e2e persists across restarts

- GIVEN postgres is running with `mybudget_e2e` present and containing migrated schema
- WHEN the container is restarted
- THEN `mybudget_e2e` is still accessible (volume preserved)

---

### Requirement: Supporting Services

Redis MUST use the `redis:7-alpine` image. Mailpit, Seq, and Jaeger MUST use their official images at pinned or `latest` tags. All services MUST define restart policies appropriate for local dev (`unless-stopped`). Web UIs MUST be accessible on well-known host ports: Mailpit on `8025`, Seq on `5341`, Jaeger UI on `16686`.

#### Scenario: Mailpit UI is accessible after start

- GIVEN the `infra` profile is running
- WHEN a browser navigates to `http://localhost:8025`
- THEN the Mailpit web interface loads

#### Scenario: Seq UI is accessible after start

- GIVEN the `infra` profile is running
- WHEN a browser navigates to `http://localhost:5341`
- THEN the Seq log UI loads

---

### Requirement: Secrets Isolation

The `.env` file MUST be listed in `.gitignore` and MUST NOT be committed to the repository. A `.env.example` file MUST exist at the repo root documenting all required variables with placeholder values. `docker-compose.yml` MUST NOT contain any literal secret values.

#### Scenario: .env is not tracked by git

- GIVEN `.env` contains PostgreSQL credentials
- WHEN `git status` is checked after adding `.env`
- THEN `.env` appears as untracked (ignored) and is not staged

#### Scenario: .env.example documents all required variables

- GIVEN a developer clones the repository
- WHEN they read `.env.example`
- THEN all variables required by `docker-compose.yml` are listed with descriptions and example values

---

### Requirement: E2E Database Provisioning Convention

`mybudget_e2e` MUST be documented as automatically provisioned via docker init SQL. `mybudget_test` provisioning MUST be documented as owned by `IntegrationTestFactory` via `EnsureDeletedAsync()` — no manual creation is required. `mybudget` (dev) MUST be provisioned via `.env` as before.

#### Scenario: Developer onboarding creates all three databases

- GIVEN a developer has Docker Desktop running and has run `docker compose --profile infra up -d`
- WHEN they inspect the postgres instance
- THEN `mybudget_e2e` exists (created by init SQL) and `mybudget` exists (from `.env`); `mybudget_test` is created on first integration test run
