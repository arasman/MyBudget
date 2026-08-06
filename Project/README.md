# MyBudget

Personal finance management application — .NET 10 API + Vue 3 frontend.

> See the [repo-root README](../README.md) for the full TFM overview (features, tech stack, demo credentials, project status). This document covers local development workflow specifically.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/)
- [pnpm](https://pnpm.io/installation) (frontend package manager — no npm or yarn)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- dotnet-ef tool:

```bash
dotnet tool install --global dotnet-ef
```

## Quick Start

### 1. Start infrastructure

```bash
docker compose --profile infra up -d
```

This starts PostgreSQL, Redis, Mailpit, Seq, and Jaeger.

### 2. Configure secrets

Copy the environment template and configure your local values:

```bash
cp .env.example .env
```

Set the local development connection string via User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=mybudget;Username=mybudget;Password=mybudget" \
  --project src/MyBudget.Api
```

### 3. Run the API

```bash
dotnet run --project src/MyBudget.Api
```

The API migrates the database automatically on startup. Available at `http://localhost:5184` (or `https://localhost:7228`).

### 4. Run the frontend

```bash
cd frontend
pnpm install
pnpm dev
```

Open http://localhost:5173

## Project Structure

```
Project/
├── src/
│   ├── MyBudget.Api/            # ASP.NET Core host — Program.cs, middleware, appsettings
│   ├── MyBudget.Features/       # Vertical Slice Architecture — business logic
│   │   ├── Features/            # Auth, BudgetStructure, BudgetExecution, CurrentSituation, Dashboard, ...
│   │   ├── SharedKernel/        # Entities, EF Core persistence, Auth, Caching, Email, Results
│   │   ├── Behaviours/          # Mediator pipeline (Validation, Logging, Caching)
│   │   └── Migrations/          # EF Core migrations
│   └── MyBudget.Gateway/        # YARP reverse-proxy API gateway
├── tests/
│   ├── MyBudget.Features.Tests/     # Unit tests (xUnit, SQLite in-memory)
│   └── MyBudget.Integration.Tests/  # Integration tests (WebApplicationFactory, real Postgres)
├── frontend/
│   ├── src/
│   │   ├── features/            # Feature-folder slices mirroring the backend
│   │   ├── components/, stores/, layouts/, views/, router/, i18n/, api/, utils/, types/
│   └── e2e/                     # Playwright specs
├── docker-compose.yml            # Local infra: Postgres, Redis, Mailpit, Seq, Jaeger
└── scripts/db/                   # Seed & cleanup SQL for demo data
```

## Architecture

### Vertical Slice Architecture (VSA)

Each feature is a self-contained slice under `Features/<Area>/<UseCase>/` with exactly 4 files:
1. `{UseCase}Command.cs` / `{UseCase}Query.cs` — request definition (`IRequest<Result<T>>`)
2. `{UseCase}Handler.cs` — the handler implementation
3. `{UseCase}Validator.cs` — FluentValidation rules
4. `{UseCase}Endpoint.cs` — minimal API endpoint with a static `Map(IEndpointRouteBuilder)` method

**Rules:**
- Slices NEVER reference each other — only SharedKernel
- SharedKernel types are only created when used by 3+ slices
- Handlers return `Result<T>` (see `SharedKernel/Results/Result.cs`)

### Pipeline Behaviours (in order)

1. `ValidationBehaviour` — runs FluentValidation before the handler executes
2. `LoggingBehaviour` — structured request/response logging (Serilog)
3. `CachingBehaviour` — read-through cache for cacheable queries (currently a no-op — see [Known Limitations](#known-limitations))

### Tailwind v4 CSS-Only (Intentional)

There is NO `tailwind.config.js` — this is by design (ADR-004). Tailwind v4 is configured
entirely in CSS via `frontend/src/assets/main.css` using `@import`, `@plugin`, and `@theme` directives.
daisyUI v5 is configured with `@plugin "daisyui"`. Do not add a `tailwind.config.js`.

## EF Core Migration Workflow

### Add a migration

Run from the `Project/` directory:

```bash
dotnet ef migrations add <MigrationName> --project src/MyBudget.Features --startup-project src/MyBudget.Api
```

### Apply manually (optional)

The app auto-migrates on startup (`MigrateAsync` in `Program.cs`). Manual apply:

```bash
dotnet ef database update --project src/MyBudget.Features --startup-project src/MyBudget.Api
```

### Multi-branch migration conflicts

If two branches add migrations concurrently, on merge the second developer must:
1. Delete their migration: `dotnet ef migrations remove --project src/MyBudget.Features --startup-project src/MyBudget.Api`
2. Pull latest `main`
3. Re-run `dotnet ef migrations add <MigrationName> ...` to get a fresh timestamp after the merged migration

## Running Tests

Backend (from `Project/`):

```bash
dotnet test
```

Frontend unit tests:

```bash
cd frontend
pnpm test
```

End-to-end (Playwright) — needs the API running against the isolated E2E database, in three terminals:

```bash
# Terminal 1 — API in E2E mode (port 5079)
dotnet run --project src/MyBudget.Api --launch-profile e2e

# Terminal 2 — frontend pointing at the E2E API
VITE_API_TARGET=http://localhost:5079 pnpm --dir frontend run dev

# Terminal 3
cd frontend && pnpm exec playwright test
```

*(Windows CMD: replace the Terminal 2 line with `set VITE_API_TARGET=http://localhost:5079 && pnpm --dir frontend run dev`.)*

## Known Limitations

1. **MigrateAsync race condition**: `MigrateAsync` in `Program.cs` causes
   a race condition on multi-instance horizontal deployments. Acceptable for TFM scope (single instance).

2. **NullCacheService**: Redis caching is deferred — `ICacheService` is registered as a no-op
   (`NullCacheService`) until the dedicated caching feature change is implemented. The pipeline
   behaviour (`CachingBehaviour`) is wired and ready; adding Redis requires only implementing
   `RedisCacheService` and swapping the DI registration in `Program.cs`.
