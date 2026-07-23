# backend-scaffold Specification

## Purpose

Define the .NET 10 solution structure, SharedKernel types, pipeline behaviours, EF Core baseline, YARP Gateway, and startup pipeline for MyBudget. All subsequent feature slices depend on this scaffold being wired correctly.

## Requirements

### Requirement: Solution Structure

The solution MUST contain exactly three projects under `Project/src/`: `MyBudget.Features` (class library), `MyBudget.Api` (web host), and `MyBudget.Gateway` (YARP reverse proxy), all targeting `net10.0`. Test stubs `MyBudget.Features.Tests` and `MyBudget.Integration.Tests` MUST exist under `Project/tests/` as empty csproj files. All projects MUST be registered in `MyBudget.sln`.

#### Scenario: Solution builds from clean state

- GIVEN the .NET 10 SDK is installed and no `bin/` or `obj/` folders exist
- WHEN `dotnet build MyBudget.sln` is executed at `Project/`
- THEN the build completes with zero errors and zero warnings

#### Scenario: Test stubs are discovered by the runner

- GIVEN both test csproj files exist with xUnit and FluentAssertions references
- WHEN `dotnet test MyBudget.sln` is executed
- THEN the runner finds both assemblies and reports zero test failures (no tests to run)

---

### Requirement: SharedKernel Types

`MyBudget.Features` MUST expose under its `SharedKernel/` namespace the following types: `BaseEntity`, `Result<T>`, `PagedList<T>`, `ICacheable`, `ICacheService`, `NullCacheService`, `IEmailSender`, `AppDbContext`, `ConnectionFactory`, `SliceActivitySource`, and the full `Email/` subtree (`EmailMessage`, `IEmailChannel`, `EmailChannel`, `EmailSenderService`). No domain logic MAY reside in SharedKernel — only infrastructure contracts and base types.

#### Scenario: Result<T> encapsulates success and failure

- GIVEN a handler returns `Result<T>.Success(value)`
- WHEN the caller inspects the result
- THEN `result.IsSuccess` is `true` and `result.Value` equals the returned value

#### Scenario: Result<T> encapsulates failure without throwing

- GIVEN a handler returns `Result<T>.Failure("error message")`
- WHEN the caller inspects the result
- THEN `result.IsFailure` is `true`, `result.Error` contains the message, and no exception is thrown

#### Scenario: NullCacheService is registered as default

- GIVEN the application starts with no Redis configuration
- WHEN `ICacheService` is resolved from DI
- THEN a `NullCacheService` instance is returned and no exception occurs

#### Scenario: PagedList<T> computes pagination metadata

- GIVEN a source collection of 25 items with page size 10
- WHEN `PagedList<T>.Create(source, pageNumber: 2, pageSize: 10)` is called
- THEN `TotalCount` is 25, `TotalPages` is 3, `HasPreviousPage` is true, `HasNextPage` is true

---

### Requirement: Pipeline Behaviours

`MyBudget.Features` MUST include `ValidationBehaviour<TRequest, TResponse>`, `LoggingBehaviour<TRequest, TResponse>`, and `CachingBehaviour<TRequest, TResponse>` registered as Mediator pipeline behaviours. `ValidationBehaviour` MUST run FluentValidation validators and return a failure `Result` without reaching the handler when validation fails. `CachingBehaviour` MUST skip caching for requests that do not implement `ICacheable`.

#### Scenario: ValidationBehaviour short-circuits on invalid request

- GIVEN a command with a required field left empty and a FluentValidation validator registered
- WHEN the command is sent via Mediator
- THEN `ValidationBehaviour` intercepts, runs the validator, and returns `Result.Failure` containing validation errors without invoking the handler

#### Scenario: CachingBehaviour passes through non-cacheable requests

- GIVEN a command that does not implement `ICacheable`
- WHEN the command is sent via Mediator
- THEN `CachingBehaviour` calls `next()` directly with no cache read or write

#### Scenario: LoggingBehaviour records entry and exit

- GIVEN Serilog is configured and a valid command is sent
- WHEN the handler processes the command
- THEN structured log entries exist for request entry (with request type) and request exit (with elapsed time and outcome)

---

### Requirement: EF Core Baseline

`AppDbContext` MUST use the Npgsql EF Core provider. A single empty migration named `InitialCreate` MUST exist under `Project/src/MyBudget.Features/Migrations/`. The application MUST call `MigrateAsync()` on startup before handling any HTTP request, EXCEPT when `ASPNETCORE_ENVIRONMENT` is `Testing` — in that environment `MigrateAsync()` MUST NOT be called by `Program.cs` (integration test harness owns migration). E2E environment MUST call `MigrateAsync()` on startup to create schema before the E2E reset endpoint is available. All `decimal` columns MUST have global precision `(18, 2)` configured in `OnModelCreating`. No secrets (connection strings) MAY appear in `appsettings.json`. Each environment MUST load its database connection string exclusively from its environment-specific appsettings layer: `appsettings.Testing.json` MUST target `mybudget_test`; `appsettings.E2E.json` MUST target `mybudget_e2e`; Development MUST NOT load either test connection string.

#### Scenario: Migration creates the history table on a clean database

- GIVEN a PostgreSQL instance is running and the database does not exist
- WHEN the application starts in Development and `MigrateAsync()` is called
- THEN the `__EFMigrationsHistory` table is created and contains one row for `InitialCreate`

#### Scenario: MigrateAsync is skipped when environment is Testing

- GIVEN the API starts with `ASPNETCORE_ENVIRONMENT=Testing`
- WHEN the startup pipeline completes
- THEN `MigrateAsync()` is NOT called by `Program.cs` (test harness owns migration)

#### Scenario: MigrateAsync is called when environment is E2E

- GIVEN the API starts with `ASPNETCORE_ENVIRONMENT=E2E`
- WHEN the startup pipeline completes
- THEN `MigrateAsync()` IS called by `Program.cs` to create schema before reset endpoint

#### Scenario: E2E environment loads mybudget_e2e connection string

- GIVEN `appsettings.E2E.json` exists with `DefaultConnection` pointing to `mybudget_e2e`
- WHEN the API starts with `ASPNETCORE_ENVIRONMENT=E2E`
- THEN `AppDbContext` and `ConnectionFactory` both resolve to `mybudget_e2e`

#### Scenario: Testing environment loads mybudget_test connection string

- GIVEN `appsettings.Testing.json` exists with `DefaultConnection` pointing to `mybudget_test`
- WHEN the API starts with `ASPNETCORE_ENVIRONMENT=Testing`
- THEN `AppDbContext` resolves to `mybudget_test`

#### Scenario: Decimal precision is enforced globally

- GIVEN an entity with a `decimal` property is added to the model
- WHEN the EF Core model is built
- THEN the column has precision 18 and scale 2 without requiring per-property configuration

---

### Requirement: Program.cs Middleware Pipeline Order

`MyBudget.Api/Program.cs` MUST register and use middleware in the following order:
1. Serilog request logging
2. `AddFeatures` (registers Mediator, behaviours, validators, DbContext, services)
3. Localization (`UseRequestLocalization`)
4. Auth stubs (`UseAuthentication`, `UseAuthorization`)
5. Exception handling middleware
6. Endpoint mapping (auto-discovered via reflection on `IEndpoint`)
7. OpenAPI / Scalar

No middleware MUST be registered between `UseAuthentication` and `UseAuthorization`.

#### Scenario: Endpoints are auto-discovered on startup

- GIVEN one or more classes implementing `IEndpoint` exist in `MyBudget.Features`
- WHEN the application starts
- THEN all `IEndpoint.Map(IEndpointRouteBuilder)` methods are called via reflection without manual registration

#### Scenario: Startup fails fast when database is unreachable

- GIVEN the PostgreSQL connection string is invalid
- WHEN `MigrateAsync()` is called during startup
- THEN the application throws before accepting any HTTP requests and logs a fatal error

---

### Requirement: YARP Gateway

`MyBudget.Gateway` MUST proxy all requests matching `/api/**` to `MyBudget.Api`. A `CorrelationId` header MUST be injected on every forwarded request using a YARP transform. No secrets MAY appear in the gateway's `appsettings.json`.

#### Scenario: Request is forwarded with CorrelationId

- GIVEN the gateway is running and `MyBudget.Api` is the upstream
- WHEN an HTTP request arrives at the gateway for `/api/health`
- THEN the request is forwarded and the upstream receives an `X-Correlation-Id` header containing a non-empty value

#### Scenario: Gateway rejects unknown routes

- GIVEN no route matches the incoming path
- WHEN the gateway receives a request for `/unknown`
- THEN the gateway returns HTTP 404 without forwarding

---

### Requirement: User Secrets

`MyBudget.Api` MUST be initialized with `dotnet user-secrets init`. All connection strings and sensitive values MUST be stored in User Secrets for local development. `appsettings.json` MUST contain only non-sensitive defaults (e.g., log level, localization settings).

#### Scenario: Application starts without appsettings secrets

- GIVEN `appsettings.json` contains no connection strings and User Secrets holds the PostgreSQL connection string
- WHEN the application starts in Development environment
- THEN the connection string is resolved from User Secrets and the database is reachable

---

### Requirement: Integration Test Factory — Config-Driven Connection

`IntegrationTestFactory` MUST read `DefaultConnection` from `IConfiguration` (resolved from `appsettings.Testing.json`) and MUST NOT use a hardcoded connection string constant. The factory MUST call `EnsureDeletedAsync()` + `MigrateAsync()` per test collection to own schema state for `mybudget_test`.

#### Scenario: Factory reads connection string from config

- GIVEN `appsettings.Testing.json` contains `DefaultConnection` pointing to `mybudget_test`
- WHEN `IntegrationTestFactory` initializes
- THEN it connects to `mybudget_test` without any hardcoded constant

#### Scenario: Factory wipes and migrates per collection

- GIVEN a new test collection starts
- WHEN `InitializeAsync` runs
- THEN `EnsureDeletedAsync()` is called followed by `MigrateAsync()` and the schema is current
