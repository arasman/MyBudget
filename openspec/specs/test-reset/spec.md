# test-reset Specification

## Purpose

Define the `POST /api/test/reset` endpoint and its environment-gated registration. This endpoint enables test harnesses (integration and E2E) to wipe and re-migrate test databases in isolation from production and development environments.

## Requirements

### Requirement: Environment-Gated Reset Endpoint

`POST /api/test/reset` MUST exist and MUST be registered in `Program.cs` only when `ASPNETCORE_ENVIRONMENT` is `Testing` or `E2E`. The check MUST occur at registration time, not at request time. The endpoint MUST wipe the database schema via `DROP SCHEMA public CASCADE; CREATE SCHEMA public;` followed by `MigrateAsync()` on `AppDbContext` and return HTTP 200 on success. The endpoint MUST NOT be reachable in any environment that is not `Testing` or `E2E`.

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
