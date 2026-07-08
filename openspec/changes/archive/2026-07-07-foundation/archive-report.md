# Archive Report: foundation

**Change**: foundation
**Archived**: 2026-07-07
**Result**: PASS WITH WARNINGS (0 CRITICAL)
**Verify Result**: PASS — 0 critical, 6 warnings (3 fixed post-verify, 3 non-actionable), 4 suggestions

---

## What Was Built

Full-stack greenfield scaffold: .NET 10 VSA solution + Vue 3 frontend + Docker Compose infra + git repository initialization.

Delivered four capabilities:
- **backend-scaffold**: .NET 10 solution with 5 projects (MyBudget.Features, MyBudget.Api, MyBudget.Gateway, and 2 test stubs). Full SharedKernel types (BaseEntity, Result<T>, PagedList<T>, caching, email, persistence). Pipeline behaviours (ValidationBehaviour, LoggingBehaviour, CachingBehaviour). Program.cs wired with Serilog, Mediator, middleware, EF Core, and User Secrets.
- **frontend-scaffold**: Vue 3 + Vite + TypeScript skeleton. Tailwind v4 CSS-only (no tailwind.config.ts). daisyUI v5 themes (light/dark). Pinia stores, vue-router, vue-i18n (EN/ES), Axios client with interceptors. ESLint flat config + Prettier + Vitest setup.
- **infra-local**: Docker Compose with postgres:16, redis:7-alpine, mailpit, seq, jaeger. Profiles: infra (all services), full (services + api). Environment-driven config via .env/.env.example.
- **git-setup**: Repository initialized at repo root with main branch, feature/foundation branch, comprehensive .gitignore.

---

## Warnings Fixed Post-Verify

**[W003] YARP CorrelationId transform — literal header value**
- Issue: YARP config-based transforms don't support dynamic UUID injection
- Fix: Removed literal transform; CorrelationIdMiddleware in API generates real UUID per request
- Spec still met: X-Correlation-Id header is present on forwarded requests

**[W004] ConnectionFactory keyed DI registration**
- Issue: Implementation used AddSingleton (unkeyed) instead of AddKeyedSingleton("postgres")
- Fix: Changed to AddKeyedSingleton<ConnectionFactory>("postgres")
- Impact: Future query handlers can now use [FromKeyedServices("postgres")] correctly

**[W005] Axios Authorization header async fire-and-forget pattern**
- Issue: Interceptor returned before async import() resolved
- Fix: Changed to synchronous import with await; Authorization header now sets properly
- Non-blocking at foundation: auth is always false, but MUST be fixed for auth feature

---

## Warnings Remaining (Non-Actionable)

**[W001] Microsoft.OpenApi 2.0.0 — NuGet transitive vulnerability**
- Severity: High (GHSA-v5pm-xwqc-g5wc)
- Mitigation: Awaiting upstream fix. Future .NET updates will resolve.
- Scope: Does not affect foundation functionality; noted for future patch cycles

**[W002] SQLitePCLRaw.lib.e_sqlite3 2.1.10 — NuGet transitive vulnerability**
- Severity: High (GHSA-2m69-gcr7-jv3q)
- Mitigation: Awaiting upstream fix. Transitive from Microsoft.EntityFrameworkCore.Sqlite.
- Scope: Foundation code does not use SQLite in production (Npgsql only); used in unit tests

**[W006] Email type naming inconsistency (cosmetic)**
- Issue: Spec mentions IEmailChannel and EmailSenderService; implementation uses EmailChannel and EmailBackgroundService
- Impact: Naming only; no runtime impact
- Rationale: Task spec uses implementation naming; consistency is acceptable

---

## Suggestions

**[S001]** Solution file is .slnx (new XML format); tasks and design reference .sln — minor inconsistency in documentation.

**[S002]** main.ts plugin initialization order is correct (pinia before router) even though spec text differed.

**[S003]** Design doc shows http/client.ts path; implementation correctly uses src/api/axios.ts per spec.

**[S004]** Consider adding packages.lock.json to prevent NuGet version drift on restore.

---

## Capabilities Delivered

| Capability | Files | Status | Spec Compliance |
|------------|-------|--------|-----------------|
| backend-scaffold | 5 projects, 40+ files, SharedKernel, Behaviours, Program.cs, Middleware | Complete | 17 scenarios: 12 compliant, 5 partial (runtime-only) |
| frontend-scaffold | Vite scaffold, 7 directories, i18n, router, stores, Axios, ESLint, Prettier, Vitest | Complete | 20 scenarios: 17 compliant, 3 partial (runtime-only) |
| infra-local | docker-compose.yml, .env.example, 5 services, 2 profiles | Complete | 6 scenarios: 3 compliant, 3 partial (Docker runtime) |
| git-setup | Repository, main branch, feature/foundation, .gitignore | Complete | 4 scenarios: 4 compliant, 0 partial |

---

## Tasks Completion

- Total tasks: 41 across 12 phases
- Completed: 41 (100%)
- Failed: 0
- All phases executed in dependency order

---

## Build & Verification

- `dotnet build MyBudget.sln`: Succeeded (12 NU1903 warnings — transitive vulnerabilities)
- `dotnet test MyBudget.sln`: Passed (0 failures, both test stubs discovered)
- `pnpm vitest run`: Passed (no test files, exit code 0)
- `pnpm lint`: Passed (ESLint flat config, 0 errors)
- `pnpm build`: Succeeded (Vue + Vite build, no errors)

---

## Next Change

**auth** — User registration, login, JWT token issuance, email invitation system, role-based access (4 roles per budget).

This change builds on the foundation scaffold. Required prerequisites:
- JWT token handling in frontend (update W005 fix from foundation)
- Keyed DI for ConnectionFactory (W004 fix from foundation)
- Auth domain slice with User entity, Password hashing (BCrypt), Email verification
- Token refresh mechanism
- Role inheritance model

---

## Archive Structure

```
D:/Projects/bigschool/TFM/MyBudget/openspec/changes/archive/2026-07-07-foundation/
├── proposal.md                          ← Intent, scope, approach, risks, rollback
├── design.md                            ← Technical architecture, decisions (ADR-001 through ADR-006)
├── tasks.md                             ← 41 tasks across 12 phases, dependency graph
├── verify-report.md                     ← 0 critical, 6 warnings, 4 suggestions, spec coverage
├── archive-report.md                    ← This file
└── specs/
    ├── backend-scaffold/spec.md         ← Solution, SharedKernel, Behaviours, EF, YARP, User Secrets
    ├── frontend-scaffold/spec.md        ← Folder structure, Tailwind v4, Axios, i18n, Router, ESLint
    ├── infra-local/spec.md              ← Docker Compose profiles, PostgreSQL, services, secrets
    └── git-setup/spec.md                ← Repository init, .gitignore, branches, commit convention
```

---

## Source of Truth Updated

Main specs now reflect the foundation scaffold:
- `D:/Projects/bigschool/TFM/MyBudget/openspec/specs/backend-scaffold/spec.md`
- `D:/Projects/bigschool/TFM/MyBudget/openspec/specs/frontend-scaffold/spec.md`
- `D:/Projects/bigschool/TFM/MyBudget/openspec/specs/infra-local/spec.md`
- `D:/Projects/bigschool/TFM/MyBudget/openspec/specs/git-setup/spec.md`

These specs are now the authoritative definition for the foundation. All subsequent changes (auth, features, etc.) build on these specs and may extend them.

---

## SDD Cycle Complete

The foundation change has successfully completed all phases:

1. **Proposal** ✓ — Intent and scope defined
2. **Specs** ✓ — Four capabilities specified (backend-scaffold, frontend-scaffold, infra-local, git-setup)
3. **Design** ✓ — Architecture locked down with ADRs and file contracts
4. **Tasks** ✓ — 41 atomic tasks across 12 phases with dependency graph
5. **Apply** ✓ — All 41 tasks implemented and completed
6. **Verify** ✓ — Verification passed with 0 critical issues
7. **Archive** ✓ — Change archived with full audit trail

The foundation is ready for the next feature slice (auth).
