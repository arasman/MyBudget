# MyBudget

**Personal & family budget management, replacing the spreadsheet.**

MyBudget is a full-stack web application for planning, tracking, and comparing budgets across multiple cycles and periods, with multi-user, role-based access per budget and multi-currency support. Built as a Trabajo Fin de Máster (TFM) project.

> Originally built to replace a family budget spreadsheet with something that supports multiple budgets, shared access with role-based permissions (Owner / Admin / Operator / Read-only), recurring and one-off expense tracking, and historical trend visualization.

[![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/10.0)
[![Vue](https://img.shields.io/badge/Vue-3.5-4FC08D?logo=vuedotjs)](https://vuejs.org/)
[![TypeScript](https://img.shields.io/badge/TypeScript-6.0-3178C6?logo=typescript)](https://www.typescriptlang.org/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql)](https://www.postgresql.org/)
[![License](https://img.shields.io/badge/license-MIT-green)](#license)

---

## Table of Contents

- [Features](#features)
- [Tech Stack](#tech-stack)
- [Architecture](#architecture)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Demo Credentials](#demo-credentials)
- [Testing](#testing)
- [EF Core Migrations](#ef-core-migrations)
- [Configuration Reference](#configuration-reference)
- [Deployment](#deployment)
- [Project Status](#project-status)
- [Known Limitations](#known-limitations)
- [License](#license)

---

## Features

**Accounts & access**
- Registration, JWT login/refresh/logout, forgot/reset password, forced password change, account lockout after failed attempts
- Multiple budgets per user; invite other users by email with a role per budget
- Four roles per budget: Owner, Admin, Operator, Read-only

**Budget structure**
- Cycles (e.g. a year) and Periods (e.g. months) inside a cycle, with per-cycle exchange rates
- Category groups and categories
- Budget lines with date-range validity and a gapless revision history (change an amount mid-cycle without losing the audit trail)
- Drag-and-drop reordering, inline editing, soft delete with restore everywhere

**Execution (actual spending)**
- Record expenses, credit notes, and debit notes against budget lines, in any currency with a per-entry exchange rate
- Multi-period budget matrix view with inline CRUD and progressive per-period loading
- Currency toggle and inline exchange-rate editing across the whole matrix

**Current situation & dashboard**
- Bank account catalog per budget
- Daily "cut record" snapshots (bank balances vs. budgeted/executed totals), multi-currency
- Read-only dashboard: lifetime trend charts, average-behavior bands, per-period and cross-cycle BudgetLine comparisons

**Cross-cutting**
- Full audit log (entity mutations) and a separate security event log with retention policy
- Ephemeral toast notifications for every mutating action, with show-deleted/restore UX
- Full English/Spanish localization, switchable at login and in-app

Planned next (MVP B, not yet built): projects, financial commitments, installment/debt tracking, import/export.

## Tech Stack

| Layer | Technology |
|---|---|
| Backend | .NET 10 · ASP.NET Core Minimal APIs · Mediator (source-generated) · Dapper + EF Core 10 (Npgsql) · FluentValidation · Mapster · BCrypt.Net · MailKit · Serilog + Seq · OpenTelemetry + Jaeger · Polly · StackExchange.Redis · YARP (API gateway) |
| Frontend | Vue 3.5 (Composition API) · TypeScript · Vite · Pinia · vue-router · vue-i18n · Tailwind CSS v4 + daisyUI v5 · Chart.js (vue-chartjs) · Axios · Zod |
| Database | PostgreSQL 16 |
| Testing | xUnit + NSubstitute + Shouldly (backend unit) · WebApplicationFactory (backend integration, real Postgres) · Vitest + Testing Library (frontend unit) · Playwright (E2E) |
| Infrastructure | Docker Compose (Postgres, Redis, Mailpit, Seq, Jaeger) |
| Package managers | NuGet · pnpm (no npm/yarn) |

## Architecture

**Backend — Vertical Slice Architecture (VSA).** Each use case is a self-contained slice under `Features/<Area>/<UseCase>/` with exactly four files: request+handler, FluentValidation validator, minimal-API endpoint, and request/response DTOs. Slices never reference each other directly — shared types only move into `SharedKernel` once genuinely used by 3+ slices. `AppDbContext` stays pure (no `IMediator` injection).

Mediator pipeline behaviours run in this order for every request: **ValidationBehaviour → LoggingBehaviour → CachingBehaviour**.

**Frontend — feature-folder pattern** mirroring the backend slices (`src/features/<area>/{api,components,composables,stores,types,views,__tests__}`), with a shared `AppLayout` (authenticated) and `PublicLayout` (auth screens).

**Styling — Tailwind v4, CSS-only (ADR-004).** There is intentionally no `tailwind.config.js`; Tailwind v4 and daisyUI v5 are configured entirely in `frontend/src/assets/main.css` via `@import`, `@plugin`, and `@theme` directives.

**Gateway.** `MyBudget.Gateway` is a thin YARP reverse proxy in front of the API — not required for local development, relevant for deployment topology.

**Delivery process.** The whole feature set was built via Spec-Driven Development — every change has a proposal, spec, design, and task breakdown under [`openspec/`](openspec/ROADMAP.md), archived on completion. 23 changes shipped to date; see the [Project Status](#project-status) section.

## Project Structure

```
MyBudget/
├── openspec/                   # Spec-Driven Development artifacts
│   ├── ROADMAP.md              # Full feature history (MVP A complete, MVP B planned)
│   └── changes/archive/        # 23 archived proposal/spec/design/tasks sets
└── Project/                    # Application source
    ├── src/
    │   ├── MyBudget.Api/            # ASP.NET Core host — Program.cs, middleware, appsettings
    │   ├── MyBudget.Features/       # VSA business logic
    │   │   ├── Features/            # Auth, BudgetStructure, BudgetExecution, CurrentSituation, Dashboard, ...
    │   │   ├── SharedKernel/        # Entities, EF Core persistence, Auth, Caching, Email, Results
    │   │   ├── Behaviours/          # Mediator pipeline (Validation, Logging, Caching)
    │   │   └── Migrations/          # EF Core migrations
    │   └── MyBudget.Gateway/        # YARP reverse-proxy gateway
    ├── tests/
    │   ├── MyBudget.Features.Tests/     # Unit tests (xUnit, SQLite in-memory)
    │   └── MyBudget.Integration.Tests/  # Integration tests (WebApplicationFactory, real Postgres)
    ├── frontend/
    │   ├── src/
    │   │   ├── features/            # bank-accounts, budget-execution, budget-structure, current-situation, dashboard
    │   │   ├── components/, stores/, layouts/, views/, router/, i18n/, api/, utils/, types/
    │   └── e2e/                     # Playwright specs (36 specs across 8 feature areas)
    ├── docker-compose.yml           # Local infra: Postgres, Redis, Mailpit, Seq, Jaeger
    └── scripts/db/                  # Seed & cleanup SQL for demo data
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 20+](https://nodejs.org/)
- [pnpm](https://pnpm.io/installation) — the frontend uses pnpm exclusively, no npm/yarn
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- EF Core CLI tool: `dotnet tool install --global dotnet-ef`

### Quick start

```bash
git clone <repo-url>
cd MyBudget/Project
```

**1. Start infrastructure** (PostgreSQL, Redis, Mailpit, Seq, Jaeger):

```bash
docker compose --profile infra up -d
```

**2. Configure environment and secrets**

```bash
cp .env.example .env
```

Defaults in `.env.example` work out of the box for local dev (Postgres user/db `mybudget`, standard ports). Then point the API at the database via User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=mybudget;Username=mybudget;Password=mybudget" \
  --project src/MyBudget.Api
```

**3. Run the API** — migrates the database automatically on startup:

```bash
dotnet run --project src/MyBudget.Api
```

API available at `http://localhost:5184` (or `https://localhost:7228`).

**4. Run the frontend**

```bash
cd frontend
pnpm install
pnpm dev
```

Open **http://localhost:5173**. Register a new account, or use the [demo credentials](#demo-credentials) below.

<details>
<summary>Alternative: run the API in Docker too</summary>

```bash
docker compose --profile full up -d
```

This builds and runs the API container alongside the infra services — skip step 3 above. Still run the frontend separately with `pnpm dev`.
</details>

## Demo Credentials

A seed script provisions a demo user with sample budget data (used to populate the dashboard's trend charts, which need history to be meaningful):

```bash
docker exec -i -e PGPASSWORD=mybudget project-postgres-1 psql -U mybudget -d mybudget \
  < scripts/db/seed_dashboard_demo.sql
```

| | |
|---|---|
| Email | `seed-demo@mybudget.local` |
| Password | `DemoPass123!` |

To remove the demo data: `scripts/db/cleanup_dashboard_demo.sql` (same invocation pattern).

## Testing

**Backend** (from `Project/`):

```bash
dotnet test
```

**Frontend unit tests**:

```bash
cd frontend
pnpm test
```

**End-to-end (Playwright)** — needs the API running against an isolated E2E database, in three terminals:

```bash
# Terminal 1 — API in E2E mode (port 5079)
dotnet run --project src/MyBudget.Api --launch-profile e2e

# Terminal 2 — frontend pointing at the E2E API
VITE_API_TARGET=http://localhost:5079 pnpm --dir frontend run dev

# Terminal 3
cd frontend && pnpm exec playwright test
```

*(Windows CMD: replace the Terminal 2 line with `set VITE_API_TARGET=http://localhost:5079 && pnpm --dir frontend run dev`.)*

## EF Core Migrations

```bash
# Add a migration
dotnet ef migrations add <MigrationName> --project src/MyBudget.Features --startup-project src/MyBudget.Api

# Apply manually (optional — the API also auto-migrates on startup)
dotnet ef database update --project src/MyBudget.Features --startup-project src/MyBudget.Api
```

If two branches add migrations concurrently, on merge the second developer should delete their migration, pull latest, and re-add it to get a fresh timestamp after the merged one.

## Configuration Reference

| Service | Default port | Notes |
|---|---|---|
| Frontend (Vite dev server) | `5173` | `VITE_API_TARGET` overrides the `/api` proxy target |
| API — HTTP | `5184` | |
| API — HTTPS | `7228` | |
| API — E2E profile | `5079` | `ASPNETCORE_ENVIRONMENT=E2E` |
| Gateway (YARP) | `5031` / `7093` (HTTPS) | Not required for local dev |
| PostgreSQL | `5432` | `POSTGRES_PORT` in `.env` |
| Redis | `6379` | `REDIS_PORT` in `.env` |
| Mailpit (SMTP / UI) | `1025` / `8025` | dev email capture — no real email sent locally |
| Seq (log UI) | `5341` | `SEQ_PORT` in `.env` |
| Jaeger (trace UI) | `16686` | `JAEGER_UI_PORT` in `.env` |

All ports are overridable via `.env`.
Other notable settings (`Project/src/MyBudget.Api/appsettings.json`): 
- JWT access tokens expire in 15 minutes.
- Supported locales are `en`/`es`.
- Audit log retention is 90 days.
- Account lockout after 5 failed attempts for 30 minutes.

## Deployment

See [`docs/DEPLOYMENT.md`](docs/DEPLOYMENT.md) for the full guide (Hetzner + Caddy + Brevo).

## Project Status

**MVP A — complete.** 22 feature changes shipped and archived, from initial scaffold through the analytics dashboard (2026-07-07 → 2026-08-04). Full history in [`openspec/ROADMAP.md`](openspec/ROADMAP.md).

**MVP B — planned, not started.** Projects, financial commitments, installment/debt tracking, import/export.

## Known Limitations

1. **`MigrateAsync` race condition.** Auto-migration on API startup is not safe for multi-instance horizontal deployments. Acceptable for this project's scope (single instance).
2. **Caching not yet implemented.** `ICacheService` is currently a no-op (`NullCacheService`); the `CachingBehaviour` pipeline stage is wired and ready — enabling Redis caching only requires implementing `RedisCacheService` and swapping the DI registration.

## License

[MIT](LICENSE)

## Author

Alejandro Rafael Alfaro Soto — Trabajo Fin de Máster - 2026.
