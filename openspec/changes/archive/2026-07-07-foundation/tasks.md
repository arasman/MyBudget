# Tasks: Foundation Scaffold

> Generated from spec + design artifacts. Each task is atomic and completable in one session.
> Parallel tasks within a phase can be executed simultaneously; sequential tasks must wait for predecessors.

---

## Phase 1: Git + Repository Initialization

> Prerequisite for all other phases. Must complete before any files are created.

### [x] 1.1 Initialize git repository and create first commit on main

- **What**: Run `git init` at `D:/Projects/bigschool/TFM/MyBudget/`, set default branch to `main`, create `.gitignore` at repo root with all required exclusions, stage and commit with message `chore(repo): initialize repository with gitignore`
- **Files**:
  - `D:/Projects/bigschool/TFM/MyBudget/.gitignore`
- **Verify**: `git log --oneline` shows at least one commit; `git branch` shows `main`
- **Spec ref**: git-setup › Repository Initialisation › "Scenario: Repository exists with main branch"

### [x] 1.2 Create and checkout feature/foundation branch

- **What**: Run `git checkout -b feature/foundation` from the `main` branch after task 1.1 commit exists
- **Files**: none (branch only)
- **Verify**: `git branch` shows `* feature/foundation`
- **Spec ref**: git-setup › Repository Initialisation › "Scenario: Foundation work is on feature branch"
- **Depends on**: 1.1

---

## Phase 2: .NET Solution + Project Scaffold

> Sequential within the phase (each step builds on the previous). Can start after phase 1.

### [x] 2.1 Create solution file and src/tests directory structure

- **What**: Run `dotnet new sln -n MyBudget` inside `Project/`, then create `Project/src/` and `Project/tests/` directories
- **Files**:
  - `Project/MyBudget.sln`
- **Verify**: `MyBudget.sln` file exists at `Project/`
- **Spec ref**: backend-scaffold › Solution Structure › "Scenario: Solution builds from clean state"
- **Depends on**: 1.2

### [x] 2.2 Create MyBudget.Features class library project

- **What**: Run `dotnet new classlib -n MyBudget.Features -f net10.0` under `Project/src/`, then `dotnet sln add src/MyBudget.Features/MyBudget.Features.csproj`; remove default `Class1.cs`
- **Files**:
  - `Project/src/MyBudget.Features/MyBudget.Features.csproj`
- **Verify**: `dotnet build src/MyBudget.Features` succeeds
- **Spec ref**: backend-scaffold › Solution Structure
- **Depends on**: 2.1

### [x] 2.3 Create MyBudget.Api web host project

- **What**: Run `dotnet new web -n MyBudget.Api -f net10.0` under `Project/src/`, then add to solution; add project reference to `MyBudget.Features`
- **Files**:
  - `Project/src/MyBudget.Api/MyBudget.Api.csproj`
  - `Project/src/MyBudget.Api/Program.cs` (placeholder — will be replaced in phase 6)
  - `Project/src/MyBudget.Api/appsettings.json`
  - `Project/src/MyBudget.Api/appsettings.Development.json`
- **Verify**: `dotnet build src/MyBudget.Api` succeeds
- **Spec ref**: backend-scaffold › Solution Structure
- **Depends on**: 2.2

### [x] 2.4 Create MyBudget.Gateway YARP proxy project

- **What**: Run `dotnet new web -n MyBudget.Gateway -f net10.0` under `Project/src/`, add to solution; do NOT add reference to MyBudget.Features (gateway is independent)
- **Files**:
  - `Project/src/MyBudget.Gateway/MyBudget.Gateway.csproj`
  - `Project/src/MyBudget.Gateway/Program.cs` (placeholder)
  - `Project/src/MyBudget.Gateway/appsettings.json`
- **Verify**: `dotnet build src/MyBudget.Gateway` succeeds
- **Spec ref**: backend-scaffold › Solution Structure
- **Depends on**: 2.1

### [x] 2.5 Create MyBudget.Features.Tests stub project

- **What**: Run `dotnet new xunit -n MyBudget.Features.Tests -f net10.0` under `Project/tests/`, add to solution; add project reference to `MyBudget.Features`; remove default `UnitTest1.cs`
- **Files**:
  - `Project/tests/MyBudget.Features.Tests/MyBudget.Features.Tests.csproj`
- **Verify**: `dotnet build tests/MyBudget.Features.Tests` succeeds
- **Spec ref**: backend-scaffold › Solution Structure › "Scenario: Test stubs are discovered by the runner"
- **Depends on**: 2.2

### [x] 2.6 Create MyBudget.Integration.Tests stub project

- **What**: Run `dotnet new xunit -n MyBudget.Integration.Tests -f net10.0` under `Project/tests/`, add to solution; add project reference to `MyBudget.Api`; remove default `UnitTest1.cs`
- **Files**:
  - `Project/tests/MyBudget.Integration.Tests/MyBudget.Integration.Tests.csproj`
- **Verify**: `dotnet build tests/MyBudget.Integration.Tests` succeeds
- **Spec ref**: backend-scaffold › Solution Structure › "Scenario: Test stubs are discovered by the runner"
- **Depends on**: 2.3

---

## Phase 3: NuGet Packages

> Tasks 3.1–3.4 are PARALLEL (each targets a different project). Task 3.5 is sequential (test projects, depends on 3.1).

### [x] 3.1 Add NuGet packages to MyBudget.Features

- **What**: Add the following packages via `dotnet add package` to `MyBudget.Features`:
  - `Mediator` (Zarathos source-gen mediator)
  - `FluentValidation`
  - `Microsoft.EntityFrameworkCore`
  - `Npgsql.EntityFrameworkCore.PostgreSQL`
  - `Dapper`
  - `Npgsql`
  - `Serilog`
  - `Serilog.Extensions.Hosting`
  - `Microsoft.Extensions.Localization`
  - `MailKit`
  - `OpenTelemetry`
  - `OpenTelemetry.Extensions.Hosting`
  - `OpenTelemetry.Instrumentation.AspNetCore`
- **Files**: `Project/src/MyBudget.Features/MyBudget.Features.csproj`
- **Verify**: `dotnet restore src/MyBudget.Features` exits cleanly; no NU1101 errors
- **Spec ref**: backend-scaffold › SharedKernel Types; Pipeline Behaviours; EF Core Baseline
- **Depends on**: 2.2

### [x] 3.2 Add NuGet packages to MyBudget.Api

- **What**: Add to `MyBudget.Api`:
  - `Serilog.AspNetCore`
  - `Serilog.Sinks.Console`
  - `Serilog.Sinks.Seq`
  - `Microsoft.EntityFrameworkCore.Design` (for EF CLI tooling)
  - `Scalar.AspNetCore` (OpenAPI/Scalar UI)
- **Files**: `Project/src/MyBudget.Api/MyBudget.Api.csproj`
- **Verify**: `dotnet restore src/MyBudget.Api` exits cleanly
- **Spec ref**: backend-scaffold › Program.cs Middleware Pipeline Order
- **Depends on**: 2.3

### [x] 3.3 Add NuGet packages to MyBudget.Gateway

- **What**: Add to `MyBudget.Gateway`:
  - `Yarp.ReverseProxy`
- **Files**: `Project/src/MyBudget.Gateway/MyBudget.Gateway.csproj`
- **Verify**: `dotnet restore src/MyBudget.Gateway` exits cleanly
- **Spec ref**: backend-scaffold › YARP Gateway
- **Depends on**: 2.4

### [x] 3.4 Add NuGet packages to test projects

- **What**: Add to `MyBudget.Features.Tests`:
  - `NSubstitute`
  - `Shouldly`
  - `Microsoft.EntityFrameworkCore.Sqlite` (for in-memory unit tests)
  Add to `MyBudget.Integration.Tests`:
  - `Microsoft.AspNetCore.Mvc.Testing`
- **Files**:
  - `Project/tests/MyBudget.Features.Tests/MyBudget.Features.Tests.csproj`
  - `Project/tests/MyBudget.Integration.Tests/MyBudget.Integration.Tests.csproj`
- **Verify**: `dotnet restore MyBudget.sln` exits cleanly for all projects
- **Spec ref**: backend-scaffold › Solution Structure › "Scenario: Test stubs are discovered by the runner"
- **Depends on**: 2.5, 2.6

### [x] 3.5 Verify full solution builds after all packages added

- **What**: Run `dotnet build MyBudget.sln` and confirm zero errors, zero warnings
- **Files**: none (verification only)
- **Verify**: Exit code 0, no warnings or errors in output
- **Spec ref**: backend-scaffold › Solution Structure › "Scenario: Solution builds from clean state"
- **Depends on**: 3.1, 3.2, 3.3, 3.4

---

## Phase 4: SharedKernel Types

> Tasks 4.1–4.7 are PARALLEL (each creates an independent file). Task 4.8 is sequential.

### [x] 4.1 Create BaseEntity

- **What**: Create `BaseEntity.cs` with `Id` (Guid), `CreatedAt` (DateTimeOffset), `UpdatedAt` (DateTimeOffset?), `DomainEvents` list, `AddDomainEvent()`, and `ClearDomainEvents()` as per the design contract
- **Files**: `Project/src/MyBudget.Features/SharedKernel/Entities/BaseEntity.cs`
- **Verify**: `dotnet build src/MyBudget.Features` succeeds with no errors
- **Spec ref**: backend-scaffold › SharedKernel Types; design ADR-006
- **Depends on**: 3.1

### [x] 4.2 Create Result<T>

- **What**: Create sealed `Result<T>` with `IsSuccess`, `IsFailure`, `Value`, `Error`, static `Success(T)` and `Failure(string)` factory methods; no exceptions thrown
- **Files**: `Project/src/MyBudget.Features/SharedKernel/Results/Result.cs`
- **Verify**: `dotnet build src/MyBudget.Features` succeeds; Result<T> scenarios from spec are representable
- **Spec ref**: backend-scaffold › SharedKernel Types › "Scenario: Result<T> encapsulates success and failure" + "Scenario: Result<T> encapsulates failure without throwing"
- **Depends on**: 3.1

### [x] 4.3 Create PagedList<T>

- **What**: Create `PagedList<T>` with `Items`, `TotalCount`, `PageNumber`, `PageSize`, computed `TotalPages`, `HasPreviousPage`, `HasNextPage`; static `Create(IQueryable<T>, int, int)` factory
- **Files**: `Project/src/MyBudget.Features/SharedKernel/Pagination/PagedList.cs`
- **Verify**: `dotnet build` succeeds; pagination metadata math matches spec scenario (25 items, page 2 of 10 → TotalPages=3, HasPrev=true, HasNext=true)
- **Spec ref**: backend-scaffold › SharedKernel Types › "Scenario: PagedList<T> computes pagination metadata"
- **Depends on**: 3.1

### [x] 4.4 Create Caching abstractions (ICacheable, ICacheService, NullCacheService)

- **What**: Create `ICacheable.cs` (marker interface with `CacheKey` string and `CacheDuration` TimeSpan), `ICacheService.cs` (GetAsync, SetAsync, RemoveAsync), `NullCacheService.cs` (always returns cache miss, no-ops for set/remove)
- **Files**:
  - `Project/src/MyBudget.Features/SharedKernel/Caching/ICacheable.cs`
  - `Project/src/MyBudget.Features/SharedKernel/Caching/ICacheService.cs`
  - `Project/src/MyBudget.Features/SharedKernel/Caching/NullCacheService.cs`
- **Verify**: `dotnet build` succeeds; `NullCacheService` implements `ICacheService`
- **Spec ref**: backend-scaffold › SharedKernel Types › "Scenario: NullCacheService is registered as default"; design ADR-005
- **Depends on**: 3.1

### [x] 4.5 Create Email subtree (IEmailSender, EmailMessage, EmailChannel, EmailBackgroundService)

- **What**: Create:
  - `IEmailSender.cs` — `Task SendAsync(EmailMessage, CancellationToken)`
  - `EmailMessage.cs` — record/class with `To`, `Subject`, `Body` (HTML string)
  - `EmailChannel.cs` — wraps `Channel<EmailMessage>`, implements `IEmailSender` by writing to channel
  - `EmailBackgroundService.cs` — `BackgroundService` that reads from channel and sends via MailKit SMTP
- **Files**:
  - `Project/src/MyBudget.Features/SharedKernel/Email/IEmailSender.cs`
  - `Project/src/MyBudget.Features/SharedKernel/Email/EmailMessage.cs`
  - `Project/src/MyBudget.Features/SharedKernel/Email/EmailChannel.cs`
  - `Project/src/MyBudget.Features/SharedKernel/Email/EmailBackgroundService.cs`
- **Verify**: `dotnet build` succeeds; no reference to `System.Net.Mail` (use MailKit only)
- **Spec ref**: backend-scaffold › SharedKernel Types
- **Depends on**: 3.1

### [x] 4.6 Create Persistence types (AppDbContext, ConnectionFactory)

- **What**: Create:
  - `AppDbContext.cs` — derives from `DbContext`, constructor takes `DbContextOptions<AppDbContext>`, `OnModelCreating` sets global decimal precision (18,2) and calls `ApplyConfigurationsFromAssembly`; NO IMediator injection
  - `ConnectionFactory.cs` — keyed DI wrapper for Npgsql `IDbConnection`; key `"postgres"`; returns open `NpgsqlConnection`
- **Files**:
  - `Project/src/MyBudget.Features/SharedKernel/Persistence/AppDbContext.cs`
  - `Project/src/MyBudget.Features/SharedKernel/Persistence/ConnectionFactory.cs`
- **Verify**: `dotnet build` succeeds; `AppDbContext` has no IMediator dependency
- **Spec ref**: backend-scaffold › EF Core Baseline › "Scenario: Decimal precision is enforced globally"
- **Depends on**: 3.1

### [x] 4.7 Create SliceActivitySource

- **What**: Create `SliceActivitySource.cs` — static class exposing a named `ActivitySource` for OpenTelemetry tracing of slice handlers
- **Files**: `Project/src/MyBudget.Features/SharedKernel/Telemetry/SliceActivitySource.cs`
- **Verify**: `dotnet build` succeeds
- **Spec ref**: backend-scaffold › SharedKernel Types
- **Depends on**: 3.1

### [x] 4.8 Verify full SharedKernel builds cleanly

- **What**: Run `dotnet build src/MyBudget.Features` and confirm zero errors and zero warnings across all SharedKernel types
- **Files**: none (verification only)
- **Verify**: Exit code 0, no warnings
- **Spec ref**: backend-scaffold › Solution Structure
- **Depends on**: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7

---

## Phase 5: Pipeline Behaviours + Extensions

> Tasks 5.1–5.3 are PARALLEL. Task 5.4 and 5.5 are sequential.

### [x] 5.1 Create ValidationBehaviour

- **What**: Create `ValidationBehaviour<TRequest, TResponse>` implementing Mediator's `IPipelineBehavior`. Resolve all `IValidator<TRequest>` from DI; if any fail, return `Result.Failure` with concatenated error messages without invoking `next()`
- **Files**: `Project/src/MyBudget.Features/Behaviours/ValidationBehaviour.cs`
- **Verify**: `dotnet build` succeeds; short-circuit logic is present
- **Spec ref**: backend-scaffold › Pipeline Behaviours › "Scenario: ValidationBehaviour short-circuits on invalid request"
- **Depends on**: 4.8

### [x] 5.2 Create LoggingBehaviour

- **What**: Create `LoggingBehaviour<TRequest, TResponse>` implementing `IPipelineBehavior`. Log request entry with request type name; start `SliceActivitySource` span; call `next()`; log exit with elapsed time and success/failure outcome using Serilog structured logging
- **Files**: `Project/src/MyBudget.Features/Behaviours/LoggingBehaviour.cs`
- **Verify**: `dotnet build` succeeds; both entry and exit log statements are present
- **Spec ref**: backend-scaffold › Pipeline Behaviours › "Scenario: LoggingBehaviour records entry and exit"
- **Depends on**: 4.8

### [x] 5.3 Create CachingBehaviour

- **What**: Create `CachingBehaviour<TRequest, TResponse>` implementing `IPipelineBehavior`. If `TRequest` does not implement `ICacheable`, call `next()` and return. Otherwise, check `ICacheService.GetAsync`; on hit return cached value; on miss call `next()` and set cache
- **Files**: `Project/src/MyBudget.Features/Behaviours/CachingBehaviour.cs`
- **Verify**: `dotnet build` succeeds; non-ICacheable requests pass through without cache interaction
- **Spec ref**: backend-scaffold › Pipeline Behaviours › "Scenario: CachingBehaviour passes through non-cacheable requests"
- **Depends on**: 4.8

### [x] 5.4 Create ServiceCollectionExtensions (AddFeatures)

- **What**: Create `ServiceCollectionExtensions.cs` with `AddFeatures(IServiceCollection, IConfiguration)` extension method that registers: Mediator (with all three behaviours in order: Validation → Logging → Caching), FluentValidation validators from Features assembly, `AppDbContext` (Npgsql provider, connection string from config), `ConnectionFactory` (keyed as `"postgres"`), `ICacheService` → `NullCacheService`, `IEmailSender` → `EmailChannel`, `EmailBackgroundService` as hosted service, `AddLocalization()`, OpenTelemetry with `SliceActivitySource`
- **Files**: `Project/src/MyBudget.Features/Extensions/ServiceCollectionExtensions.cs`
- **Verify**: `dotnet build` succeeds; all service registrations compile
- **Spec ref**: backend-scaffold › Program.cs Middleware Pipeline Order; SharedKernel Types › "Scenario: NullCacheService is registered as default"
- **Depends on**: 5.1, 5.2, 5.3

### [x] 5.5 Create EndpointExtensions (MapAllSliceEndpoints)

- **What**: Create `EndpointExtensions.cs` with `MapAllSliceEndpoints(IEndpointRouteBuilder)` extension method that uses reflection to find all types in `MyBudget.Features` assembly with a public static `void Map(IEndpointRouteBuilder)` method and invokes them
- **Files**: `Project/src/MyBudget.Features/Extensions/EndpointExtensions.cs`
- **Verify**: `dotnet build` succeeds; reflection scan logic present
- **Spec ref**: backend-scaffold › Program.cs Middleware Pipeline Order › "Scenario: Endpoints are auto-discovered on startup"
- **Depends on**: 4.8

---

## Phase 6: Program.cs + Middleware

> Tasks 6.1 and 6.2 are PARALLEL. Task 6.3 and 6.4 are sequential.

### [x] 6.1 Create CorrelationIdMiddleware

- **What**: Create `CorrelationIdMiddleware.cs` — reads `X-Correlation-Id` header from request (or generates UUID if absent), stores in `HttpContext.Items`, and stamps the response header
- **Files**: `Project/src/MyBudget.Api/Middleware/CorrelationIdMiddleware.cs`
- **Verify**: `dotnet build` succeeds
- **Spec ref**: backend-scaffold › Program.cs Middleware Pipeline Order
- **Depends on**: 3.2

### [x] 6.2 Create ExceptionMiddleware

- **What**: Create `ExceptionMiddleware.cs` — wraps `next(context)` in try/catch; on unhandled exception logs fatal error via ILogger and returns `ProblemDetails` JSON with HTTP 500 status
- **Files**: `Project/src/MyBudget.Api/Middleware/ExceptionMiddleware.cs`
- **Verify**: `dotnet build` succeeds; returns `application/problem+json` content type
- **Spec ref**: backend-scaffold › Program.cs Middleware Pipeline Order
- **Depends on**: 3.2

### [x] 6.3 Write full Program.cs for MyBudget.Api

- **What**: Replace placeholder `Program.cs` with the full pipeline wiring in the exact order specified by design section 3:
  1. `UseSerilog` (read from configuration)
  2. `AddFeatures(builder.Configuration)`
  3. `AddAuthentication()` + `AddAuthorization()` stubs
  4. `app = builder.Build()`
  5. `await MigrateAsync()` before `app.Run()`
  6. `UseRequestLocalization()`
  7. `UseAuthentication()` + `UseAuthorization()`
  8. `UseMiddleware<CorrelationIdMiddleware>()`
  9. `UseMiddleware<ExceptionMiddleware>()`
  10. `MapAllSliceEndpoints()`
  11. `MapOpenApi()` (dev only)
  12. `app.Run()`
- **Files**: `Project/src/MyBudget.Api/Program.cs`
- **Verify**: `dotnet build src/MyBudget.Api` succeeds with zero errors
- **Spec ref**: backend-scaffold › Program.cs Middleware Pipeline Order (all scenarios)
- **Depends on**: 5.4, 5.5, 6.1, 6.2

### [x] 6.4 Configure appsettings.json and initialize User Secrets

- **What**:
  - Set `appsettings.json` with non-sensitive defaults: Serilog log levels, localization `DefaultRequestCulture: "en"` with supported cultures `["en", "es"]`, Seq URL pointing to localhost:5341 — NO connection strings
  - Set `appsettings.Development.json` with development-specific overrides (verbose logging)
  - Run `dotnet user-secrets init` on `MyBudget.Api`
  - Set placeholder User Secret: `dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=mybudget;Username=mybudget;Password=mybudget"`
- **Files**:
  - `Project/src/MyBudget.Api/appsettings.json`
  - `Project/src/MyBudget.Api/appsettings.Development.json`
- **Verify**: `cat appsettings.json` contains NO connection string literals; `dotnet user-secrets list` in MyBudget.Api shows the connection string key
- **Spec ref**: backend-scaffold › User Secrets (all scenarios)
- **Depends on**: 2.3

---

## Phase 7: EF Core Migration

> Sequential — depends on AppDbContext and Program.cs being complete.

### [x] 7.1 Generate InitialCreate migration

- **What**: Run `dotnet ef migrations add InitialCreate --project src/MyBudget.Features --startup-project src/MyBudget.Api --output-dir Migrations` from `Project/` directory
- **Files**:
  - `Project/src/MyBudget.Features/Migrations/[timestamp]_InitialCreate.cs`
  - `Project/src/MyBudget.Features/Migrations/[timestamp]_InitialCreate.Designer.cs`
  - `Project/src/MyBudget.Features/Migrations/AppDbContextModelSnapshot.cs`
- **Verify**: Migration files exist under `Migrations/`; `dotnet build` still succeeds
- **Spec ref**: backend-scaffold › EF Core Baseline › "Scenario: Migration creates the history table on a clean database"
- **Depends on**: 6.3

---

## Phase 8: YARP Gateway

> Parallel to phase 7 (both depend on 6.x but not on each other).

### [x] 8.1 Wire YARP in MyBudget.Gateway/Program.cs

- **What**: Replace placeholder `Program.cs` with YARP configuration: `builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))`. Add CorrelationId header inject transform
- **Files**: `Project/src/MyBudget.Gateway/Program.cs`
- **Verify**: `dotnet build src/MyBudget.Gateway` succeeds
- **Spec ref**: backend-scaffold › YARP Gateway › "Scenario: Request is forwarded with CorrelationId"
- **Depends on**: 3.3

### [x] 8.2 Configure YARP routes in appsettings.json

- **What**: Write gateway `appsettings.json` with YARP `ReverseProxy` section: one route matching `{**catch-all}` on path `/api/{**remainder}` pointing to the `api-cluster`; cluster destination `http://localhost:5000`; transform to inject `X-Correlation-Id` header; NO literal secrets
- **Files**: `Project/src/MyBudget.Gateway/appsettings.json`
- **Verify**: `appsettings.json` parses as valid JSON; YARP route and cluster sections present
- **Spec ref**: backend-scaffold › YARP Gateway › "Scenario: Gateway rejects unknown routes"
- **Depends on**: 8.1

---

## Phase 9: Docker Compose

> Parallel to phases 7 and 8.

### [x] 9.1 Create .env.example

- **What**: Create `.env.example` at `Project/` root documenting all required variables: `POSTGRES_USER`, `POSTGRES_PASSWORD`, `POSTGRES_DB`, `POSTGRES_PORT` (5432), `REDIS_PORT` (6379), `MAILPIT_SMTP_PORT` (1025), `MAILPIT_UI_PORT` (8025), `SEQ_PORT` (5341), `SEQ_ACCEPT_EULA`, `JAEGER_UI_PORT` (16686); include description comments
- **Files**: `Project/.env.example`
- **Verify**: File exists; all variables listed; no real secrets present
- **Spec ref**: infra-local › Secrets Isolation › "Scenario: .env.example documents all required variables"
- **Depends on**: 1.2 (must exist on feature/foundation branch)

### [x] 9.2 Create docker-compose.yml

- **What**: Create `docker-compose.yml` at `Project/` with:
  - `postgres:16-alpine` (profiles: [infra, full], port 5432, volume `postgres-data`, env_file: .env, restart: unless-stopped)
  - `redis:7-alpine` (profiles: [infra, full], port 6379, restart: unless-stopped)
  - `axllent/mailpit:latest` (profiles: [infra, full], ports 1025 SMTP + 8025 UI, restart: unless-stopped)
  - `datalust/seq:latest` (profiles: [infra, full], port 5341, volume `seq-data`, `SEQ_FIRSTRUN_ADMINPASSWORDHASH` from env_file, restart: unless-stopped)
  - `jaegertracing/all-in-one:latest` (profiles: [infra, full], port 16686 UI, restart: unless-stopped)
  - `api` service (profiles: [full], build: ./src/MyBudget.Api, depends_on all five infra services)
  - Named volumes: `postgres-data`, `seq-data`
  - Network: `mybudget-network` (bridge driver)
  - No literal credential values in this file
- **Files**: `Project/docker-compose.yml`
- **Verify**: `docker compose config` parses without errors; no service starts without `--profile` flag
- **Spec ref**: infra-local › Docker Compose Profiles (all scenarios); infra-local › PostgreSQL Service; infra-local › Supporting Services
- **Depends on**: 9.1

---

## Phase 10: Frontend Skeleton (Vite + Packages + Config)

> Can start in parallel with phases 7–9 (independent of backend changes).

### [x] 10.1 Scaffold Vite + Vue 3 + TypeScript project

- **What**: Run `pnpm create vite@latest frontend --template vue-ts` inside `Project/`; ensure `pnpm-lock.yaml` is the only lockfile; set `"packageManager": "pnpm@..."` in `package.json`
- **Files**:
  - `Project/frontend/package.json`
  - `Project/frontend/vite.config.ts`
  - `Project/frontend/tsconfig.json`
  - `Project/frontend/index.html`
  - `Project/frontend/src/main.ts`
  - `Project/frontend/src/App.vue`
- **Verify**: `pnpm install` succeeds; no `package-lock.json` or `yarn.lock` present
- **Spec ref**: frontend-scaffold › Package Manager Constraint
- **Depends on**: 1.2

### [x] 10.2 Install frontend dependencies

- **What**: Run `pnpm add` for: `tailwindcss@next`, `@tailwindcss/vite`, `daisyui`, `pinia`, `vue-router`, `vue-i18n`, `axios`, `zod`, `dompurify`; and `pnpm add -D` for: `vitest`, `@testing-library/vue`, `eslint`, `@eslint/js`, `eslint-plugin-vue`, `@typescript-eslint/eslint-plugin`, `@typescript-eslint/parser`, `prettier`, `eslint-config-prettier`, `@types/dompurify`, `jsdom`, `@vitejs/plugin-vue`
- **Files**: `Project/frontend/package.json`, `Project/frontend/pnpm-lock.yaml`
- **Verify**: `pnpm install` exits cleanly; `pnpm list` shows all packages present
- **Spec ref**: frontend-scaffold › Tailwind v4; ESLint and Prettier; Vitest; Axios; vue-i18n; Routing
- **Depends on**: 10.1

### [x] 10.3 Configure Tailwind v4 CSS-only (no tailwind.config.ts)

- **What**: Update `vite.config.ts` to add `@tailwindcss/vite` plugin; replace boilerplate `src/assets/` with `src/assets/main.css` containing exactly `@import "tailwindcss"; @plugin "daisyui";` with a comment block explaining Tailwind v4 CSS-only config; add daisyUI themes config `light` and `dark` in the CSS; ensure NO `tailwind.config.ts` or `postcss.config.js` is created
- **Files**:
  - `Project/frontend/vite.config.ts`
  - `Project/frontend/src/assets/main.css`
- **Verify**: `pnpm dev` starts without CSS errors; `pnpm build` succeeds; no `tailwind.config.ts` exists
- **Spec ref**: frontend-scaffold › Tailwind v4 CSS-Only Configuration (all scenarios); design ADR-004
- **Depends on**: 10.2

### [x] 10.4 Configure path alias (@/) in vite.config.ts and tsconfig

- **What**: Add `resolve.alias: { '@': path.resolve(__dirname, 'src') }` to `vite.config.ts`; add `paths: { "@/*": ["./src/*"] }` and `baseUrl: "."` to `tsconfig.app.json` (or `tsconfig.json`)
- **Files**:
  - `Project/frontend/vite.config.ts`
  - `Project/frontend/tsconfig.app.json`
- **Verify**: `pnpm build` resolves an `@/` import without error
- **Spec ref**: frontend-scaffold › Path Alias
- **Depends on**: 10.3

### [x] 10.5 Configure ESLint flat config

- **What**: Create `eslint.config.ts` with flat config format; set `vue/no-v-html` = `error` and `@typescript-eslint/no-explicit-any` = `error`; integrate `eslint-config-prettier` to avoid Prettier conflicts; add `"lint": "eslint src"` and `"lint:fix": "eslint src --fix"` scripts to `package.json`
- **Files**:
  - `Project/frontend/eslint.config.ts`
  - `Project/frontend/package.json` (scripts)
- **Verify**: `pnpm lint` exits with code 0 on scaffold files
- **Spec ref**: frontend-scaffold › ESLint and Prettier (all scenarios)
- **Depends on**: 10.2

### [x] 10.6 Configure Prettier

- **What**: Create `.prettierrc` at `Project/frontend/` with standard config: `semi: false, singleQuote: true, tabWidth: 2, trailingComma: 'es5', printWidth: 100`; add `"format": "prettier --write src"` script to `package.json`
- **Files**:
  - `Project/frontend/.prettierrc`
  - `Project/frontend/package.json` (scripts)
- **Verify**: `pnpm format` runs without errors
- **Spec ref**: frontend-scaffold › ESLint and Prettier
- **Depends on**: 10.2

### [x] 10.7 Configure Vitest

- **What**: Create `vitest.config.ts` with `environment: 'jsdom'`, `globals: true`, include pattern `src/**/*.{test,spec}.ts`; add `"test": "vitest run"` script to `package.json`
- **Files**:
  - `Project/frontend/vitest.config.ts`
  - `Project/frontend/package.json` (scripts)
- **Verify**: `pnpm vitest run` exits with code 0 (no test files found = 0 failures)
- **Spec ref**: frontend-scaffold › Vitest Test Infrastructure
- **Depends on**: 10.2

### [x] 10.8 Create src/ directory skeleton

- **What**: Create the required subdirectories under `Project/frontend/src/`: `components/`, `stores/`, `router/`, `i18n/`, `api/`, `views/`, `types/`; add `.gitkeep` to empty directories if needed to track them in git
- **Files**: All seven subdirectories under `Project/frontend/src/`
- **Verify**: All seven directories exist under `src/`
- **Spec ref**: frontend-scaffold › Folder Structure › "Scenario: Expected directories exist after scaffold"
- **Depends on**: 10.1

---

## Phase 11: Frontend i18n + Stores + Router + Axios

> Tasks 11.1–11.4 are PARALLEL. Task 11.5 depends on 11.1 and 11.3. Task 11.6 depends on all of them.

### [x] 11.1 Create i18n locale files and initialization

- **What**: Create:
  - `src/i18n/locales/en.json` with `common.*` (appName, loading, error, save, cancel, confirm) and `auth.*` (login, logout, email, password, loginButton, loginError) keys
  - `src/i18n/locales/es.json` with matching Spanish translations for all keys in `en.json`
  - `src/i18n/index.ts` — creates `i18n` instance with `legacy: false`, reads `localStorage.getItem('locale') ?? 'en'`, `fallbackLocale: 'en'`, imports both locale files
- **Files**:
  - `Project/frontend/src/i18n/locales/en.json`
  - `Project/frontend/src/i18n/locales/es.json`
  - `Project/frontend/src/i18n/index.ts`
- **Verify**: `pnpm build` succeeds; `i18n.global.locale.value` defaults to `'en'` when no localStorage entry
- **Spec ref**: frontend-scaffold › vue-i18n Internationalisation (all scenarios)
- **Depends on**: 10.8

### [x] 11.2 Create Axios client singleton

- **What**: Create `src/api/axios.ts` exporting a single `axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL })` instance; attach request interceptor that sets `X-Correlation-Id` (crypto.randomUUID()), `Accept-Language` (from localStorage or `'en'`), and `Authorization: Bearer {token}` only when token is present (reads from `useAuthStore` — import lazily to avoid circular dep)
- **Files**: `Project/frontend/src/api/axios.ts`
- **Verify**: `pnpm build` succeeds; interceptor code present; no Authorization header when no token
- **Spec ref**: frontend-scaffold › Axios HTTP Client (all scenarios)
- **Depends on**: 10.8

### [x] 11.3 Create Pinia stores (auth.store.ts, locale.store.ts)

- **What**: Create:
  - `src/stores/auth.store.ts` — `useAuthStore` with `isAuthenticated = false`, `user = null`, `token: string | null = null` (stub, no JWT logic)
  - `src/stores/locale.store.ts` — `useLocaleStore` with `locale` (persisted to localStorage), `setLocale(lang)` that updates `i18n.global.locale`, `localStorage`, and adds `Accept-Language` header to the Axios default headers
- **Files**:
  - `Project/frontend/src/stores/auth.store.ts`
  - `Project/frontend/src/stores/locale.store.ts`
- **Verify**: `pnpm build` succeeds
- **Spec ref**: frontend-scaffold › vue-i18n Internationalisation › "Scenario: Locale switch updates rendered text"; design › Pinia Stores
- **Depends on**: 10.8

### [x] 11.4 Create placeholder views (LoginView.vue, HomeView.vue)

- **What**: Create:
  - `src/views/LoginView.vue` — minimal template with `<h1>{{ $t('auth.login') }}</h1>` and a styled card container using daisyUI classes
  - `src/views/HomeView.vue` — minimal template with `<h1>{{ $t('common.appName') }}</h1>` and a brief welcome message
- **Files**:
  - `Project/frontend/src/views/LoginView.vue`
  - `Project/frontend/src/views/HomeView.vue`
- **Verify**: `pnpm build` succeeds; neither view uses `v-html` or `: any`
- **Spec ref**: frontend-scaffold › Routing › "Scenario: Root path renders HomeView" + "Scenario: /login path renders LoginView"
- **Depends on**: 10.8

### [x] 11.5 Create router/index.ts

- **What**: Create `src/router/index.ts` using `createWebHistory`; define two routes: `/login` → `LoginView` (meta: `{ public: true }`), `/` → `HomeView` (meta: `{ requiresAuth: true }`); add navigation guard that redirects to `/login` if `requiresAuth && !useAuthStore().isAuthenticated`
- **Files**: `Project/frontend/src/router/index.ts`
- **Verify**: `pnpm build` succeeds; both routes defined; guard present
- **Spec ref**: frontend-scaffold › Routing (all scenarios)
- **Depends on**: 11.3, 11.4

### [x] 11.6 Create LanguageSwitcher component and wire main.ts + App.vue

- **What**: Create:
  - `src/components/LanguageSwitcher.vue` — renders EN/ES toggle buttons; calls `useLocaleStore().setLocale(lang)` on click; uses `$t('common.appName')` to verify i18n wiring
  - Update `src/main.ts` to `createApp(App).use(router).use(pinia).use(i18n).mount('#app')`
  - Update `src/App.vue` to `<RouterView />` only (no boilerplate); set `data-theme` on `<html>` from locale.store or default `'light'`
  - Import `@/assets/main.css` in `main.ts`
- **Files**:
  - `Project/frontend/src/components/LanguageSwitcher.vue`
  - `Project/frontend/src/main.ts`
  - `Project/frontend/src/App.vue`
- **Verify**: `pnpm dev` starts; app renders in browser with daisyUI light theme; EN/ES toggle updates rendered text; `pnpm lint` exits 0; `pnpm vitest run` exits 0
- **Spec ref**: frontend-scaffold › Folder Structure; Tailwind v4; vue-i18n "Scenario: Locale switch updates rendered text"
- **Depends on**: 11.1, 11.2, 11.3, 11.5

---

## Phase 12: README

> Sequential — should be written last so it reflects the actual state of all preceding tasks.

### [x] 12.1 Write Project/README.md

- **What**: Create `Project/README.md` with sections:
  1. **Prerequisites** — .NET 10 SDK, Node.js 20+, pnpm, Docker Desktop, dotnet-ef tool
  2. **Quick Start** — `docker compose --profile infra up -d`, `dotnet user-secrets set ...`, `dotnet run --project src/MyBudget.Api`, `cd frontend && pnpm install && pnpm dev`
  3. **Project Structure** — annotated directory tree (Project/src, tests, frontend, docker-compose)
  4. **Architecture** — brief VSA description, 4-file slice pattern, SharedKernel types, pipeline behaviours, Tailwind v4 CSS-only note (intentional, no tailwind.config.ts)
  5. **EF Core Migration Workflow** — commands to add a migration; multi-branch conflict resolution procedure
  6. **Known Limitations** — MigrateAsync race on multi-instance deploys; NullCacheService (Redis deferred)
  7. **Running Tests** — `dotnet test MyBudget.sln`, `pnpm vitest run`, `pnpm lint`
- **Files**: `Project/README.md`
- **Verify**: File renders correctly in a Markdown viewer; all commands listed are accurate
- **Spec ref**: proposal › Success Criteria (all items documented); design › Open Questions (Tailwind v4 note documented)
- **Depends on**: all prior phases

---

## Dependency Summary

```
Phase 1: Git
  └── Phase 2: .NET Solution (2.1–2.6)
        ├── Phase 3: NuGet Packages (3.1–3.5)
        │     └── Phase 4: SharedKernel (4.1–4.8, parallel 4.1–4.7)
        │           └── Phase 5: Behaviours + Extensions (5.1–5.5)
        │                 └── Phase 6: Program.cs + Middleware (6.1–6.4)
        │                       └── Phase 7: EF Core Migration (7.1)
        │                 └── Phase 8: YARP Gateway (8.1–8.2) [parallel to 7]
        └── Phase 9: Docker Compose (9.1–9.2) [parallel to 2–8, only needs 1.2]
        └── Phase 10: Frontend Skeleton (10.1–10.8) [parallel to 2–9, only needs 1.2]
              └── Phase 11: i18n + Stores + Router + Axios (11.1–11.6)
                    └── Phase 12: README (12.1) [sequential, needs all phases]
```

### Parallel Opportunities

| When | What can run in parallel |
|------|--------------------------|
| After Phase 1.2 | Phases 9, 10 are independent of backend — start immediately |
| Phase 4 | Tasks 4.1–4.7 are fully parallel |
| Phase 5 | Tasks 5.1–5.3 are parallel |
| Phase 6 | Tasks 6.1 and 6.2 are parallel |
| Phase 7 vs 8 | Parallel (both depend on 6.x but not on each other) |
| Phase 10 | Tasks 10.3–10.8 are parallel after 10.2 |
| Phase 11 | Tasks 11.1–11.4 are parallel |

**Total tasks**: 41 atomic tasks across 12 phases.
