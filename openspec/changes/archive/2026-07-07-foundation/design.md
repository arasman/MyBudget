# Design: Foundation Scaffold

## Technical Approach

Greenfield scaffold wired in dependency order: Git → backend solution + SharedKernel → Program.cs pipeline → Docker Compose → frontend skeleton → README. No domain logic, no feature slices — infrastructure and cross-cutting concerns only. All architectural decisions are pre-resolved by the VSA skill; this document locks down the file contracts and wiring so `sdd-tasks` and `sdd-apply` have a deterministic blueprint.

---

## 1. Directory & File Tree

```
D:/Projects/bigschool/TFM/MyBudget/
├── .gitignore                          ← repo root (excludes AnalisisInicial/, bin/, obj/, node_modules/, .env)
├── Project/
│   ├── MyBudget.sln
│   ├── README.md
│   ├── docker-compose.yml
│   ├── .env.example
│   ├── src/
│   │   ├── MyBudget.Features/
│   │   │   ├── MyBudget.Features.csproj
│   │   │   ├── SharedKernel/
│   │   │   │   ├── Entities/
│   │   │   │   │   └── BaseEntity.cs
│   │   │   │   ├── Results/
│   │   │   │   │   └── Result.cs
│   │   │   │   ├── Pagination/
│   │   │   │   │   └── PagedList.cs
│   │   │   │   ├── Caching/
│   │   │   │   │   ├── ICacheable.cs
│   │   │   │   │   ├── ICacheService.cs
│   │   │   │   │   └── NullCacheService.cs
│   │   │   │   ├── Email/
│   │   │   │   │   ├── IEmailSender.cs
│   │   │   │   │   ├── EmailMessage.cs
│   │   │   │   │   ├── EmailChannel.cs
│   │   │   │   │   └── EmailBackgroundService.cs
│   │   │   │   ├── Persistence/
│   │   │   │   │   ├── AppDbContext.cs
│   │   │   │   │   └── ConnectionFactory.cs
│   │   │   │   └── Telemetry/
│   │   │   │       └── SliceActivitySource.cs
│   │   │   ├── Behaviours/
│   │   │   │   ├── ValidationBehaviour.cs
│   │   │   │   ├── LoggingBehaviour.cs
│   │   │   │   └── CachingBehaviour.cs
│   │   │   ├── Extensions/
│   │   │   │   ├── ServiceCollectionExtensions.cs   ← AddFeatures()
│   │   │   │   └── EndpointExtensions.cs            ← MapAllSliceEndpoints()
│   │   │   └── Migrations/
│   │   │       └── 20250101000000_InitialCreate.cs  ← empty migration
│   │   ├── MyBudget.Api/
│   │   │   ├── MyBudget.Api.csproj
│   │   │   ├── Program.cs
│   │   │   ├── appsettings.json
│   │   │   ├── appsettings.Development.json
│   │   │   └── Middleware/
│   │   │       ├── CorrelationIdMiddleware.cs
│   │   │       └── ExceptionMiddleware.cs
│   │   └── MyBudget.Gateway/
│   │       ├── MyBudget.Gateway.csproj
│   │       ├── Program.cs
│   │       └── appsettings.json                     ← YARP routes config
│   └── tests/
│       ├── MyBudget.Features.Tests/
│       │   └── MyBudget.Features.Tests.csproj       ← xUnit + NSubstitute + Shouldly, no tests yet
│       └── MyBudget.Integration.Tests/
│           └── MyBudget.Integration.Tests.csproj    ← WebApplicationFactory, no tests yet
└── frontend/                                        ← Vite root (pnpm create vite)
    ├── index.html
    ├── vite.config.ts
    ├── tsconfig.json
    ├── tsconfig.app.json
    ├── tsconfig.node.json
    ├── eslint.config.ts                             ← flat config ESLint 9
    ├── .prettierrc
    ├── vitest.config.ts
    ├── package.json
    └── src/
        ├── main.ts                                  ← createApp + plugins
        ├── App.vue                                  ← RouterView + ThemeProvider
        ├── assets/
        │   └── main.css                             ← @import "tailwindcss"; @plugin "daisyui";
        ├── router/
        │   └── index.ts                             ← public + protected routes
        ├── stores/
        │   ├── auth.store.ts                        ← stub (no JWT yet)
        │   └── locale.store.ts
        ├── i18n/
        │   ├── index.ts                             ← createI18n + locale detection
        │   ├── en/
        │   │   ├── common.json
        │   │   └── auth.json
        │   └── es/
        │       ├── common.json
        │       └── auth.json
        ├── http/
        │   └── client.ts                            ← Axios singleton + interceptors
        ├── views/
        │   ├── LoginView.vue
        │   └── HomeView.vue
        └── components/
            └── LanguageSwitcher.vue
```

---

## 2. SharedKernel Wiring Diagram

```
SharedKernel Types
──────────────────

  BaseEntity
    └── (future domain entities extend this)

  Result<T>
    └── returned by ALL handlers (ValueTask<Result<T>>)

  AppDbContext  (Npgsql / EF Core)
    └── injected by COMMAND handlers only
    └── OnModelCreating → decimal precision (18,2) global convention
    └── NEVER inject IMediator here

  ConnectionFactory  (Dapper, keyed DI)
    └── injected by QUERY handlers only
    └── key: "postgres" → Npgsql connection string

  Pipeline (Mediator behaviour chain, registered in AddFeatures):
    Request
      → ValidationBehaviour   (FluentValidation, short-circuit on errors)
      → LoggingBehaviour       (Serilog + SliceActivitySource span)
      → CachingBehaviour       (checks ICacheable, calls ICacheService)
      → Handler

  ICacheService ← NullCacheService (registered at foundation)
    └── future: RedisService (caching change)

  IEmailSender ← EmailChannel (Channel<EmailMessage>)
    └── handler writes to channel (fire-and-forget)
    └── EmailBackgroundService reads channel → MailKit → SMTP
```

---

## 3. Program.cs Pipeline Sequence

```csharp
// MyBudget.Api/Program.cs — registration order (rationale inline)

var builder = WebApplication.CreateBuilder(args);

// 1. Serilog — must be first to capture all subsequent log output
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// 2. AddFeatures — registers EF, Mediator, Behaviours, Localization,
//    Email channel/background-service, OTel, ICacheService (Null)
builder.Services.AddFeatures(builder.Configuration);

// 3. AddAuthentication / AddAuthorization — stubs only (JWT wired in auth change)
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

var app = builder.Build();

// 4. MigrateAsync — runs before any request is served
await app.Services.GetRequiredService<AppDbContext>().Database.MigrateAsync();

// 5. UseRequestLocalization — before auth so error messages are already localized
app.UseRequestLocalization();

// 6. UseAuthentication + UseAuthorization
app.UseAuthentication();
app.UseAuthorization();

// 7. CorrelationIdMiddleware — stamps X-Correlation-Id on every request
app.UseMiddleware<CorrelationIdMiddleware>();

// 8. ExceptionMiddleware — catches unhandled exceptions, returns ProblemDetails
app.UseMiddleware<ExceptionMiddleware>();

// 9. MapAllSliceEndpoints — reflection scans MyBudget.Features for static Map()
app.MapAllSliceEndpoints();

// 10. MapOpenApi — dev only (Swagger UI)
app.MapOpenApi();

app.Run();
```

---

## 4. Frontend Architecture

### Component Hierarchy
```
main.ts
  └── createApp(App.vue)
        ├── router
        ├── pinia
        └── i18n

App.vue
  └── <RouterView />       ← theme data-theme attr set here from locale.store / auth.store

Routes:
  /login   → LoginView.vue   (public, no guard)
  /        → HomeView.vue    (protected, navigation guard stub → redirects to /login if !auth)
```

### Pinia Stores
| Store | File | Responsibility at Foundation |
|-------|------|------------------------------|
| `useAuthStore` | `stores/auth.store.ts` | Stub: `isAuthenticated = false`, `user = null`. No JWT logic yet. |
| `useLocaleStore` | `stores/locale.store.ts` | Active: `locale` (persisted to localStorage), `setLocale()` updates vue-i18n and Axios header. |

### Axios Client Singleton
```
http/client.ts
  export const http = axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL })

  // Request interceptor (registered ONCE at module load)
  http.interceptors.request.use(config => {
    config.headers['X-Correlation-Id'] = crypto.randomUUID()
    config.headers['Accept-Language'] = localStorage.getItem('locale') ?? 'en'
    // Future: Authorization Bearer token from auth store
    return config
  })
```

### vue-i18n Initialization Flow
```
i18n/index.ts
  1. Read localStorage.getItem('locale')
  2. If missing → detect navigator.language prefix ('es' | 'en')
  3. If unsupported → fallback to 'en'
  4. createI18n({ locale, fallbackLocale: 'en', messages: { en, es } })

LanguageSwitcher.vue
  → calls useLocaleStore().setLocale(lang)
  → updates i18n.global.locale + localStorage + Axios Accept-Language header
```

### Router Structure
```
router/index.ts
  const routes = [
    { path: '/login', component: LoginView, meta: { public: true } },
    { path: '/',      component: HomeView,  meta: { requiresAuth: true } }
  ]

  router.beforeEach((to) => {
    if (to.meta.requiresAuth && !useAuthStore().isAuthenticated) {
      return '/login'   // guard stub — full JWT check in auth change
    }
  })
```

---

## 5. Docker Compose Architecture

### Service Dependency Graph
```
  postgres ←── api (depends_on: postgres)
  redis    ←── api (depends_on: redis)
  mailpit  ←── api (depends_on: mailpit)
  seq      ←── api (depends_on: seq)
  jaeger   ←── api (depends_on: jaeger)

  gateway  ←── api (YARP upstream)
```

### Network, Volumes, Profiles
```yaml
networks:
  mybudget-network:
    driver: bridge

volumes:
  postgres-data:
  seq-data:

services:
  postgres:
    image: postgres:16
    profiles: [infra, full]
    networks: [mybudget-network]
    volumes: [postgres-data:/var/lib/postgresql/data]
    env_file: .env

  redis:
    image: redis:7-alpine
    profiles: [infra, full]
    networks: [mybudget-network]

  mailpit:
    image: axllent/mailpit:latest
    profiles: [infra, full]
    networks: [mybudget-network]

  seq:
    image: datalust/seq:latest
    profiles: [infra, full]
    networks: [mybudget-network]
    volumes: [seq-data:/data]

  jaeger:
    image: jaegertracing/all-in-one:latest
    profiles: [infra, full]
    networks: [mybudget-network]

  api:
    build: ./src/MyBudget.Api
    profiles: [full]
    networks: [mybudget-network]
    depends_on: [postgres, redis, mailpit, seq, jaeger]
    env_file: .env
```

### Environment Variable Pattern
`.env.example` committed, `.env` gitignored. Compose reads `.env` automatically. Connection strings and service URLs injected as env vars into containers. `MyBudget.Api` reads them via `IConfiguration` (env var provider overrides appsettings).

---

## 6. EF Core Migration Strategy

### Migration Location
Migrations live in `MyBudget.Features/Migrations/` — co-located with `AppDbContext` so they move together if the project is restructured.

### CLI Commands
```bash
# Add a migration (run from Project/ root)
dotnet ef migrations add InitialCreate \
  --project src/MyBudget.Features \
  --startup-project src/MyBudget.Api \
  --output-dir Migrations

# Update database (manual, optional — startup MigrateAsync handles it)
dotnet ef database update \
  --project src/MyBudget.Features \
  --startup-project src/MyBudget.Api
```

### MigrateAsync Call Site
Called in `Program.cs` immediately after `app = builder.Build()`, before `app.Run()`. Runs all pending migrations on every startup. Acceptable for TFM scope (single instance).

### Feature Branch Migration Workflow
1. Developer branches from `main` and runs `dotnet ef migrations add {FeatureName}`.
2. Migration file is committed on the feature branch.
3. On merge to `main`, if two branches added migrations concurrently, the second merger must: delete their migration, pull `main`, re-run `dotnet ef migrations add` to get a fresh timestamp after the merged one.
4. This workflow is documented in `README.md`.

### OnModelCreating Conventions
```csharp
// AppDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    // Global decimal precision — applied to ALL decimal properties
    foreach (var property in modelBuilder.Model.GetEntityTypes()
        .SelectMany(t => t.GetProperties())
        .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
    {
        property.SetPrecision(18);
        property.SetScale(2);
    }

    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

---

## 7. Architecture Decisions (ADRs)

### ADR-001: Mediator (not MediatR)

| | |
|---|---|
| **Choice** | `Mediator` NuGet package (Zarathos) |
| **Rejected** | MediatR |
| **Rationale** | Mediator generates pipeline dispatch code at compile time (source generators) — zero reflection overhead at runtime. Handlers return `ValueTask<T>` natively (better async perf). MediatR uses `object` boxing and runtime reflection. Both have identical DX for VSA; Mediator is strictly more performant. |

### ADR-002: Npgsql (prod) + SQLite (unit tests)

| | |
|---|---|
| **Choice** | Npgsql for `AppDbContext` in production/integration; SQLite in-memory for `MyBudget.Features.Tests` |
| **Rejected** | Npgsql everywhere; TestContainers |
| **Rationale** | Unit tests must not require Docker. SQLite in-memory is fast and sufficient for handler logic tests (no Npgsql-specific SQL features used in handlers). Integration tests (`MyBudget.Integration.Tests`) use a real Postgres container via TestContainers or the Docker Compose `postgres` service — that's the right layer for DB-specific behavior. |

### ADR-003: MigrateAsync() on startup

| | |
|---|---|
| **Choice** | `await db.Database.MigrateAsync()` in `Program.cs` before `app.Run()` |
| **Rejected** | Manual `dotnet ef database update` as deploy step; Flyway/Liquibase |
| **Rationale** | For TFM (single instance, PaaS deploy), having the app self-migrate on startup is simpler and eliminates out-of-sync drift. Known limitation: on multi-instance horizontal scale this causes a race condition — documented in README as acceptable for scope. |

### ADR-004: Tailwind v4 CSS-only (no tailwind.config.ts)

| | |
|---|---|
| **Choice** | `@import "tailwindcss"` + `@plugin "daisyui"` in `main.css`; `@tailwindcss/vite` Vite plugin |
| **Rejected** | Tailwind v3 with `tailwind.config.ts`; PostCSS setup |
| **Rationale** | Tailwind v4 moves all configuration into CSS. No `tailwind.config.ts` exists by design — configuration is done via `@theme`, `@layer`, `@plugin` directives in CSS. daisyUI v5 supports this natively. Less tooling surface, faster cold builds. Future devs must know this is intentional — documented in README and `main.css` header comments. |

### ADR-005: NullCacheService at foundation

| | |
|---|---|
| **Choice** | Register `NullCacheService` as `ICacheService` — always returns cache miss |
| **Rejected** | Wire Redis at foundation; skip ICacheService interface entirely |
| **Rationale** | `CachingBehaviour` and `ICacheable` are wired into the pipeline now so future cache-enabled queries add zero infrastructure cost. `NullCacheService` makes the pipeline correct (no null checks, no conditional registration) without requiring Redis to be running during foundation work. Redis is wired in the dedicated `caching` change. |

### ADR-006: Domain Events — Option B (rich domain, explicit dispatch)

| | |
|---|---|
| **Choice** | Domain events collected on `BaseEntity.DomainEvents`, dispatched explicitly by command handlers before `SaveChangesAsync()` |
| **Rejected** | Option A: dispatch in `SaveChanges` override (EF interceptor); Option C: no domain events at foundation |
| **Rationale** | Option B keeps `AppDbContext` PURE (no IMediator injection). Handlers control the dispatch timing — before or after save, depending on semantics. Option A requires injecting IMediator into EF context which violates VSA's purity rule. Option C defers the pattern and creates inconsistency across feature slices. BaseEntity carries `DomainEvents` list; foundation wires the pattern, individual feature handlers dispatch. |

---

## Data Flow

### Request → Handler (Backend)

```
HTTP Request
  → Gateway (YARP pass-through, injects X-Correlation-Id)
  → Api (CorrelationIdMiddleware stamps context)
  → ExceptionMiddleware (wraps handler chain)
  → Mediator pipeline:
      ValidationBehaviour → LoggingBehaviour → CachingBehaviour → Handler
  → Handler returns Result<T>
  → Endpoint maps Result<T> → IResult (TypedResults)
  → HTTP Response
```

### Frontend Request Flow

```
User action
  → Pinia store action
  → http/client.ts (Axios)
      → interceptor adds X-Correlation-Id + Accept-Language
  → Gateway /api/**
  → Api handler
  → Response
      → Zod schema validation (frontend)
      → Pinia state update
      → Vue reactive re-render
```

---

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `D:/Projects/bigschool/TFM/MyBudget/.gitignore` | Create | Repo root gitignore |
| `Project/MyBudget.sln` | Create | Solution with 5 projects |
| `Project/src/MyBudget.Features/*.cs` | Create | SharedKernel, Behaviours, Extensions, Migrations |
| `Project/src/MyBudget.Api/Program.cs` | Create | Full pipeline wiring |
| `Project/src/MyBudget.Api/Middleware/*.cs` | Create | CorrelationId + Exception middleware |
| `Project/src/MyBudget.Gateway/` | Create | YARP pass-through config |
| `Project/tests/**/*.csproj` | Create | Two empty test project stubs |
| `Project/frontend/` | Create | Full Vite + Vue 3 skeleton |
| `Project/docker-compose.yml` | Create | 5 infra services + api (profiles) |
| `Project/README.md` | Create | Local dev instructions |

---

## Interfaces / Contracts

```csharp
// Result<T> — used by all handlers
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public static Result<T> Success(T value) => ...;
    public static Result<T> Failure(string error) => ...;
}

// BaseEntity — all domain entities inherit this
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; protected set; }
    private readonly List<object> _domainEvents = new();
    public IReadOnlyList<object> DomainEvents => _domainEvents.AsReadOnly();
    protected void AddDomainEvent(object evt) => _domainEvents.Add(evt);
    public void ClearDomainEvents() => _domainEvents.Clear();
}

// ICacheService
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
}

// IEmailSender
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}

// Endpoint convention
public static class {Feature}Endpoint
{
    public static void Map(IEndpointRouteBuilder app) { ... }
}
```

---

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | Handler logic, Validator rules, Result<T> | xUnit + NSubstitute + Shouldly + SQLite in-memory |
| Integration | Full HTTP slice (endpoint → handler → DB) | WebApplicationFactory + real Postgres (Docker Compose `postgres` service) |
| Frontend Unit | Store logic, composables | Vitest + @testing-library/vue |
| E2E | Not in scope for foundation | Playwright (future `e2e` change) |

No tests are written in foundation — both test csproj files are stubs. Tests are written per feature change starting with `auth`.

---

## Migration / Rollout

Greenfield project — no data migration required. Rollback = delete `Project/` contents and drop the `feature/foundation` branch.

---

## Open Questions

- [ ] Confirm pnpm version compatibility with pnpm create vite for net10.0 tooling (should be fine with Node 20+, but verify at install time)
- [ ] Pin `net10.0` NuGet package versions — some packages may still be RC at time of scaffold; lock with `packages.lock.json` if any RC packages are needed
- [ ] Confirm daisyUI v5 `@plugin "daisyui"` directive syntax is stable at time of install (v5 is relatively new)
