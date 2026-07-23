# Design: DB Isolation — Three-Environment Split

## Technical Approach

Standard ASP.NET Core config layering to enforce `mybudget` (dev), `mybudget_test` (integration), `mybudget_e2e` (E2E) boundaries. A guarded reset endpoint enables test harnesses to wipe and re-migrate the DB. No new dependencies.

## Architecture Decisions

### Decision: Reset endpoint in Program.cs vs dedicated slice endpoint

| Option | Tradeoff | Chosen |
|--------|----------|--------|
| Inline `app.MapPost` in Program.cs | Simple, co-located with guard; but breaks VSA convention | |
| Dedicated `TestResetEndpoint.cs` in Features (auto-discovered by `MapAllSliceEndpoints`) | Follows existing pattern; guard moves to `Map()` body | YES |

**Rationale**: All endpoints use the `MapAllSliceEndpoints` reflection scan. Putting the reset endpoint in a `Features/Testing/TestResetEndpoint.cs` file follows the established convention. The environment guard wraps the `app.MapPost(...)` call inside `Map()` — when the env is not Testing/E2E, the method returns without registering anything, so the route does not exist.

### Decision: Docker DB provisioning — init SQL vs POSTGRES_MULTIPLE_DATABASES

| Option | Tradeoff | Chosen |
|--------|----------|--------|
| `POSTGRES_MULTIPLE_DATABASES` env var trick | Requires custom entrypoint script; fragile | |
| Init SQL in `/docker-entrypoint-initdb.d/` | Official postgres image hook; runs once on first `initdb`; simple, well-documented | YES |

**Rationale**: The postgres image runs `*.sql` files from `/docker-entrypoint-initdb.d/` alphabetically during first initialization. A single `01-create-test-dbs.sql` file with `CREATE DATABASE` statements is the simplest, most portable approach. Existing volumes must be removed once for the init to run.

### Decision: IntegrationTestFactory — config-based vs keep hardcoded const

| Option | Tradeoff | Chosen |
|--------|----------|--------|
| Keep `const TestConnectionString` | Works, but duplicates what `appsettings.Testing.json` already declares | |
| Read from `IConfiguration` chain | Single source of truth; `appsettings.Testing.json` already ships to output | YES |

**Rationale**: `appsettings.Testing.json` already contains the correct connection string. The factory should load it via `ConfigureAppConfiguration` and read `configuration["ConnectionStrings:DefaultConnection"]` instead of hardcoding. The `const` is removed. JWT and other overrides stay as in-memory config since they are test-specific values not in the JSON file.

### Decision: Playwright reset URL source

| Option | Tradeoff | Chosen |
|--------|----------|--------|
| Hardcode `http://localhost:5079` | Brittle if port changes | |
| Derive from env var `E2E_API_URL` with sensible default | Flexible, consistent with existing `E2E_BASE_URL` pattern | YES |

**Rationale**: `playwright.config.ts` already uses `process.env['E2E_BASE_URL']` for the Vite dev server. The API reset call targets the backend directly, so a separate `E2E_API_URL` env var (defaulting to `http://localhost:5079`) keeps it configurable without coupling to the frontend URL.

## Data Flow

```
Playwright globalSetup
    │
    ├─► POST http://{E2E_API_URL}/api/test/reset
    │       │
    │       ▼
    │   TestResetEndpoint.Map() [registered only in Testing/E2E]
    │       │
    │       ├─► AppDbContext.Database.EnsureDeletedAsync()
    │       └─► AppDbContext.Database.MigrateAsync()
    │
    ▼
E2E test suite runs against mybudget_e2e
    │
    ▼
Playwright globalTeardown
    │
    └─► POST http://{E2E_API_URL}/api/test/reset  (clean state)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Project/src/MyBudget.Api/appsettings.E2E.json` | Create | Connection string for `mybudget_e2e` + JWT test key |
| `Project/src/MyBudget.Api/Program.cs` | Modify | Expand migration skip: `\|\| IsEnvironment("E2E")` |
| `Project/src/MyBudget.Features/Features/Testing/TestResetEndpoint.cs` | Create | `POST /api/test/reset` — env-gated, calls `EnsureDeletedAsync` + `MigrateAsync` |
| `Project/tests/MyBudget.Integration.Tests/Infrastructure/IntegrationTestFactory.cs` | Modify | Remove `const`; load conn string from `appsettings.Testing.json` via config chain |
| `Project/frontend/e2e/global-setup.ts` | Create | Calls reset endpoint before suite |
| `Project/frontend/e2e/global-teardown.ts` | Create | Calls reset endpoint after suite |
| `Project/frontend/playwright.config.ts` | Modify | Add `globalSetup` and `globalTeardown` keys |
| `Project/docker/01-create-test-dbs.sql` | Create | `CREATE DATABASE mybudget_test; CREATE DATABASE mybudget_e2e;` |
| `Project/docker-compose.yml` | Modify | Mount `./docker/01-create-test-dbs.sql` into postgres `/docker-entrypoint-initdb.d/` |
| `openspec/config.yaml` | Modify | Line 33: fix "SQLite in-memory override" to "real Postgres (mybudget_test)" |

## Interfaces / Contracts

### TestResetEndpoint

```csharp
// Project/src/MyBudget.Features/Features/Testing/TestResetEndpoint.cs
public static class TestResetEndpoint
{
    public static IEndpointRouteBuilder Map(IEndpointRouteBuilder app)
    {
        // Guard: only register in test environments
        var env = app.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!env.IsEnvironment("Testing") && !env.IsEnvironment("E2E"))
            return app;

        app.MapPost("/api/test/reset", Handle)
            .WithTags("Testing")
            .WithName("TestReset")
            .Produces(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .AllowAnonymous();

        return app;
    }

    private static async Task<IResult> Handle(AppDbContext db)
    {
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
        return Results.Ok();
    }
}
```

### appsettings.E2E.json

```json
{
  "JWT": {
    "Key": "E2ETest-Secret-Key-MinimumLength32Characters!!"
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5432;Database=mybudget_e2e;Username=mybudget;Password=mybudget;"
  }
}
```

### globalSetup.ts / globalTeardown.ts

```typescript
// Project/frontend/e2e/global-setup.ts
import type { FullConfig } from '@playwright/test'

const API_URL = process.env['E2E_API_URL'] ?? 'http://localhost:5079'

export default async function globalSetup(_config: FullConfig) {
  const res = await fetch(`${API_URL}/api/test/reset`, { method: 'POST' })
  if (!res.ok) {
    throw new Error(
      `DB reset failed (${res.status}). Is the API running with ASPNETCORE_ENVIRONMENT=E2E?`
    )
  }
}
```

### Program.cs migration guard (modified section)

```csharp
if (!app.Environment.IsEnvironment("Testing") && !app.Environment.IsEnvironment("E2E"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}
```

### docker-compose.yml postgres volumes addition

```yaml
volumes:
  - postgres-data:/var/lib/postgresql/data
  - ./docker/01-create-test-dbs.sql:/docker-entrypoint-initdb.d/01-create-test-dbs.sql:ro
```

### 01-create-test-dbs.sql

```sql
-- Creates test databases on first postgres initialization.
-- To re-run: docker compose down -v && docker compose --profile infra up -d
CREATE DATABASE mybudget_test;
CREATE DATABASE mybudget_e2e;
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Integration | `IntegrationTestFactory` reads config | Existing test suite passes with config-based conn string |
| Integration | Reset endpoint returns 200 in Testing env | `WebApplicationFactory` test calling `POST /api/test/reset` |
| Integration | Reset endpoint absent in Development env | `WebApplicationFactory` with Development env, assert 404 |
| E2E | Full Playwright suite against `mybudget_e2e` | `globalSetup` calls reset; tests run; `globalTeardown` calls reset |
| Manual | Dev DB uncontaminated | Run full test suite, verify `mybudget` has no test-generated rows |

## Migration / Rollout

No data migration required. Docker volumes must be recreated once (`docker compose down -v && docker compose --profile infra up -d`) so the init SQL runs and creates the test databases.

## Open Questions

- None. All decisions are resolved with clear rationale.
