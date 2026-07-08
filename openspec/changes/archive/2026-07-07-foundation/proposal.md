# Proposal: Foundation Scaffold

## Intent

Establish the full-stack project scaffold for MyBudget from a greenfield state. No code exists in `Project/` today. Every subsequent feature slice depends on infrastructure that must be wired once, correctly — git history, backend solution structure, frontend skeleton, infrastructure services, and database migration baseline. Paying this cost now lets all future features focus exclusively on domain logic.

## Scope

### In Scope
- Git initialization at repo root (`D:/Projects/bigschool/TFM/MyBudget/`), `.gitignore`, branch strategy, `feature/foundation` branch
- Backend solution: `MyBudget.sln` with `MyBudget.Features`, `MyBudget.Api`, `MyBudget.Gateway` targeting `net10.0`
- SharedKernel day-1 types: `BaseEntity`, `Result<T>`, `PagedList<T>`, `ICacheable`, `ICacheService`, `NullCacheService`, `IEmailSender`, `AppDbContext` (Npgsql), `ConnectionFactory` (Dapper/keyed), `SliceActivitySource`, full `Email/` subtree
- Pipeline behaviours: `ValidationBehaviour`, `LoggingBehaviour`, `CachingBehaviour`
- `Program.cs` full pipeline wiring (Serilog → AddFeatures → Localization → Auth stubs → Middleware → Endpoints → OpenApi)
- Empty `InitialCreate` EF Core migration + `MigrateAsync()` on startup
- User Secrets init on `MyBudget.Api` with placeholder connection strings
- YARP Gateway: pass-through `/api/**` + CorrelationId inject transform
- Docker Compose with profiles: `--profile infra` (postgres, redis, mailpit, seq, jaeger) and `--profile full`
- Frontend scaffold: Vite + Vue 3 + TS, Tailwind v4 CSS-only via `@tailwindcss/vite`, daisyUI v5 (`light`/`dark`), Pinia, vue-router, vue-i18n, Axios client with interceptors, Zod, DOMPurify, ESLint flat config, Prettier, Vitest
- Locale stubs: `common.*` + `auth.*` keys in EN and ES
- Placeholder views: `LoginView.vue`, `HomeView.vue`
- Test project stubs: `MyBudget.Features.Tests` + `MyBudget.Integration.Tests` (csproj only)
- `Project/README.md` with local dev instructions

### Out of Scope
- Auth feature (JWT issuance, BCrypt, login/register slices) — deferred to `auth` change
- Any domain entity or feature slice — deferred to respective feature changes
- Production deployment config (Railway/Render/Fly.io) — deferred to `deploy` change
- Redis cache wiring beyond `NullCacheService` registration — deferred to `caching` concern
- HTTPS enforcement / TLS certificates — deferred
- CI/CD pipeline — deferred

## Capabilities

### New Capabilities
- `backend-scaffold`: .NET 10 solution, SharedKernel, Behaviours, Program.cs pipeline, EF migrations baseline
- `frontend-scaffold`: Vue 3 + Vite skeleton with i18n, routing, Axios client, Tailwind v4 + daisyUI v5
- `infra-local`: Docker Compose infrastructure services for local development

### Modified Capabilities
None — greenfield, no existing specs.

## Approach

Full foundation scaffold (Approach 2 from exploration). Greenfield means no migration risk. All architectural decisions are resolved: stack is fixed, patterns are clear from the VSA skill, package versions are pinned. Implementation proceeds area-by-area in dependency order:

1. Git setup (prerequisite for everything)
2. Backend solution + NuGet packages + SharedKernel (no domain logic, only infrastructure)
3. Program.cs pipeline + Middleware + EF migration
4. Docker Compose (infrastructure services)
5. Frontend scaffold (Vite create → replace boilerplate → wire packages → configure tooling)
6. README

No branching on approach — the VSA skill + exploration decisions make this deterministic.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `Project/` | New | All project code created here (previously empty) |
| `Project/src/MyBudget.Features/` | New | Core library: SharedKernel, Behaviours, Extensions, Migrations |
| `Project/src/MyBudget.Api/` | New | HTTP host: Program.cs, Middleware, appsettings |
| `Project/src/MyBudget.Gateway/` | New | YARP reverse proxy with pass-through config |
| `Project/tests/` | New | Two test project stubs (csproj only, no tests yet) |
| `Project/frontend/` | New | Full Vue 3 + Vite frontend skeleton |
| `Project/docker-compose.yml` | New | Infrastructure services (postgres, redis, mailpit, seq, jaeger) |
| `Project/README.md` | New | Local dev instructions |
| `.gitignore` | New | Repo root gitignore (excludes AnalisisInicial/, standard .NET + Node) |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Tailwind v4 CSS-only config confuses future devs (no `tailwind.config.ts`) | Med | Document in README; `main.css` has clear comments; daisyUI v5 uses `@plugin "daisyui"` |
| `MigrateAsync()` at startup causes race on multi-instance PaaS deploy | Low | Acceptable for TFM scope; single-instance; document as known limitation in README |
| Pinia v3 requires Vue 3.5+ — version mismatch at install time | Low | Pin `vue: ^3.5` in package.json; verify at pnpm install |
| EF Core migration conflict when two feature branches add migrations simultaneously | Med | Standard EF workflow — document re-generation procedure in README; merge discipline |
| NuGet package versions for `net10.0` may be pre-release at scaffold time | Low | Use `*` wildcard in version or pin to latest stable; lock with `packages.lock.json` if needed |

## Rollback Plan

Greenfield project — rollback means deleting the `feature/foundation` branch and the `Project/` directory contents. No existing code is affected. Git history on `main` is protected by the branch strategy (no direct pushes to `main`).

## Dependencies

- .NET 10 SDK installed locally
- Node.js 20+ and pnpm installed locally
- Docker Desktop running (for infrastructure services)
- `dotnet ef` CLI tool available (`dotnet tool install -g dotnet-ef`)

## Success Criteria

- [ ] `git log --oneline` shows at least one commit on `main` and the `feature/foundation` branch exists
- [ ] `dotnet build MyBudget.sln` succeeds with zero errors and zero warnings
- [ ] `dotnet ef database update` (or startup `MigrateAsync`) creates the `__EFMigrationsHistory` table in PostgreSQL
- [ ] `pnpm dev` in `frontend/` starts Vite dev server without errors; app renders in browser with daisyUI `light` theme
- [ ] `pnpm vitest run` exits with no failures (only the setup file, no test cases yet)
- [ ] `docker compose --profile infra up -d` starts all 5 services without errors
- [ ] ESLint passes with zero errors (`pnpm lint`)
- [ ] No secrets committed to git (User Secrets used for connection strings)
- [ ] i18n toggle works: switching locale updates rendered text in `LanguageSwitcher.vue`
- [ ] README documents all local dev steps accurately
