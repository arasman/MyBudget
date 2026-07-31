# Design: Dashboard (Budget Analytics & Charts)

## Technical Approach

Read-only query slices + one new Vue VSA module. No writes, no schema change.

- **Backend**: 3 Dapper read slices under `Features/Dashboard/`, each `Query.cs` (request + response DTOs) / `Handler.cs` / `Endpoint.cs` — 3 files per slice, no Validator (mirrors `ListPeriodExecutionTotals`). Endpoints are discovered by `app.MapAllSliceEndpoints()` reflection, so **`Program.cs` is untouched**. All use `ConnectionFactory` + `Result<T>` and `.RequireAuthorization("budget:read")` (read-only role passes).
- **Frontend**: `Project/frontend/src/features/dashboard/{api,components,composables,store,types,views,__tests__}`, mirroring `features/current-situation/`. Chart.js behind one wrapper component.

**Slice count**: 3 backend read slices (9 files). **SharedKernel additions: NONE — confirmed.** `CutRecord` already persists the 16 totals + frozen `ExchangeRate`; `ExecutionRecord` already has `PeriodId`; `BudgetLine` is `BudgetId`-scoped (not cycle-scoped), so no entity, EF configuration, or migration is added.

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|---|---|---|---|
| 1 | Default landing view | Lifetime trend/analysis view | Last-cut KPI tiles | Cut KPIs already exist in `current-situation`; the dashboard's reason to exist is behavior over time. KPI tiles stay deferred (ROADMAP `11`). |
| 2 | Density of 16 totals | **Series picker** — user selects concepts, one chart | Forced 16-line chart; 8 forced small-multiples | 16 lines are unreadable; 8 multiples force vertical scroll on mobile and remove comparison. Picker keeps one axis and lets the user build the comparison. |
| 3 | Cross-cycle `BudgetLine` identity | Match by `BudgetLineId` | Name / category fuzzy match | Verified: `BudgetLine` is scoped to `BudgetId` and carries `Revisions`, so it is literally the same row across cycles. No "unmatched line" state is needed in the query layer. |
| 4 | "Period average" | Average cut totals **within** each period, then MIN/MAX **across** period averages | Flat average of every individual cut | A period with 5 cuts would otherwise dominate a period with 1; the band must express period-to-period deviation. |
| 5 | Chart library | `chart.js` + `vue-chartjs` (already declared in `openspec/config.yaml`; not yet in `package.json`) | ECharts (bundle weight), D3 (build-it-yourself cost) | Tree-shakeable registration, canvas rendering scales to hundreds of points, smallest delta vs the declared stack. |
| 6 | Lock-in containment | Single `BaseChart.vue` wrapper: props `type`, `series`, `labels`, `axisLabel`, `conversionBasis`, `loading`, `empty`. Chart.js types never leak into stores, composables, or feature components | Import `vue-chartjs` per chart component | One replacement surface if the library is swapped; chart components stay declarative data mappers. |
| 7 | Conversion-basis isolation | Cut-frozen and transaction-time data live in **separate endpoints and separate charts**; every response carries `conversionBasis: 'cut-frozen' \| 'transaction-time'` and `BaseChart` renders it as a mandatory caption | Join both sources into one series | `CutRecord.ExchangeRate` is frozen per cut, `ExecutionRecord.ExchangeRate` is transaction-time; blending them produces silently wrong money. |
| 8 | Insufficient data | Handler (b) returns `periodCount`; client renders `InsufficientDataState` when `periodCount < 2` | Draw a degenerate band | A band from 0-1 periods has MIN == MAX == AVG and misleads. |
| 9 | Aggregation location | MIN/MAX/AVG computed in SQL (slice b); raw per-cut rows returned unaggregated (slice a) | Aggregate in the client | Band math is set-based and cheap in Postgres; raw series must stay raw for the picker to recombine client-side without refetching. |
| 10 | Responsive layout | Mobile-first single column; `lg:grid-cols-12` with charts at `lg:col-span-8` and the picker at `lg:col-span-4`; each chart inside a DaisyUI `card` with a fixed `h-[18rem] md:h-[24rem]` and `maintainAspectRatio: false`; picker collapses to a DaisyUI `dropdown` under `lg` | Chart.js responsive defaults | Chart.js needs a bounded parent height or it grows unbounded; DaisyUI card + Tailwind height class provides it and matches existing views. |

## Data Flow

    DashboardView ──→ useDashboardStore ──→ dashboardApi (axios) ──→ 3 GET endpoints
          │                   │                                            │
          │                   └── Zod parse ── typed DTOs                   ▼
          ▼                                                     Handler → ConnectionFactory
    SeriesPicker ──selectedKeys──→ useSeriesSelection ──→ BaseChart ──→ Chart.js
                                                              (cut-frozen | transaction-time)

### Sequence — dashboard load (default lifetime view)

    User        Router      DashboardView   Store        API          Handler(a/b)   Postgres
     │ /dashboard  │             │            │            │               │            │
     ├────────────►│ guard(auth) │            │            │               │            │
     │             ├────────────►│ onMounted  │            │               │            │
     │             │             ├───────────►│ load(id)   │               │            │
     │             │             │            ├─ series ──►├──────────────►├───────────►│
     │             │             │            ├─ band ────►├──────────────►├───────────►│
     │             │             │            │◄─ rows + conversionBasis ──┤◄───────────┤
     │             │             │◄─ state ───┤            │               │            │
     │             │             │ periodCount < 2 ? InsufficientDataState : bands      │
     │◄── default series preselected (TotalNet, TotalAvailable) ──────────────────────  │

### Sequence — BudgetLine comparison mode switch

    User    ComparisonModeSwitch   Store        API          Handler(c)      Postgres
     │ pick lines + mode  │          │            │              │              │
     ├───────────────────►│ mode = within-cycle | cross-cycle    │              │
     │                    ├─────────►│ resolve periodIds for mode│              │
     │                    │          ├── GET ?lineIds&periodIds ►├─────────────►│
     │                    │          │◄── per line × per period rows ───────────┤
     │                    │          │ guard: cycles' DefaultCurrencyId differ? │
     │                    │          │   → render currency-mismatch warning     │
     │◄── multi-series chart, one series per (line, cycle) ─────┤              │

## File Changes

| File | Action | Description |
|---|---|---|
| `Project/src/MyBudget.Features/Features/Dashboard/GetLifetimeCutTotals/{Query,Handler,Endpoint}.cs` | Create | Slice (a): every `CutRecord` row for a budget over `CutDate`, 16 totals + frozen rate |
| `Project/src/MyBudget.Features/Features/Dashboard/GetCutTotalsBand/{Query,Handler,Endpoint}.cs` | Create | Slice (b): per-period AVG then lifetime MIN/MAX/AVG band + `periodCount` |
| `Project/src/MyBudget.Features/Features/Dashboard/GetBudgetLineSeries/{Query,Handler,Endpoint}.cs` | Create | Slice (c): per-`BudgetLine` per-`Period` budgeted vs net, filtered by line ids + period ids |
| `Project/frontend/src/features/dashboard/api/dashboardApi.ts` | Create | 3 axios calls, `base(budgetId)` helper (cutRecordApi pattern) |
| `Project/frontend/src/features/dashboard/types/dashboard.ts` | Create | DTO types + Zod schemas + `TotalKey` union + `ConversionBasis` |
| `Project/frontend/src/features/dashboard/store/useDashboardStore.ts` | Create | Setup-store: series/band/lineSeries state, loading/error per request, mode |
| `Project/frontend/src/features/dashboard/composables/useSeriesSelection.ts` | Create | Selected `TotalKey[]`, persistence in `localStorage`, default preselect |
| `Project/frontend/src/features/dashboard/composables/useChartTheme.ts` | Create | DaisyUI CSS-variable → Chart.js color/grid mapping, reactive to theme |
| `Project/frontend/src/features/dashboard/components/BaseChart.vue` | Create | Sole `vue-chartjs` import; registers only used Chart.js controllers |
| `Project/frontend/src/features/dashboard/components/{SeriesPicker,LifetimeTotalsChart,TotalsBandChart,BudgetLineSeriesChart,ComparisonModeSwitch,InsufficientDataState}.vue` | Create | Presentational widgets, no direct Chart.js import |
| `Project/frontend/src/features/dashboard/views/DashboardView.vue` | Create | Container: BudgetTabs + responsive grid, lifetime view is default |
| `Project/frontend/src/router/index.ts` | Modify | `budgets/:budgetId/dashboard` → `Dashboard`, lazy import |
| `Project/frontend/src/features/budget-structure/components/BudgetTabs.vue` | Modify | Add Dashboard tab |
| `Project/frontend/src/i18n/locales/{en,es}.json` | Modify | `dashboard.*` keys incl. concept labels + conversion-basis captions |
| `Project/frontend/package.json` | Modify | `chart.js`, `vue-chartjs` |
| `openspec/ROADMAP.md` | Modify | Fuse `11. dashboard` + `extended-charts` |

## Interfaces / Contracts

```
GET /api/budgets/{id}/dashboard/cut-totals-series
    → { conversionBasis: "cut-frozen", points: [{ cutDate, exchangeRate, totalPositive, totalPositiveAlt, ... x16 }] }

GET /api/budgets/{id}/dashboard/cut-totals-band
    → { conversionBasis: "cut-frozen", periodCount: number,
        periods: [{ periodId, periodStart, periodEnd, avg: {<16 keys>} }],
        band:    { <16 keys>: { avg, min, max } } }

GET /api/budgets/{id}/dashboard/line-series?lineIds=&periodIds=
    → { conversionBasis: "transaction-time",
        periods: [{ periodId, cycleId, periodStart, defaultCurrencyId }],
        rows:    [{ budgetLineId, budgetLineName, periodId, budgetedAmount, netTotal }] }
```

Non-obvious SQL constraints:

- **`CutRecord` has no `PeriodId` FK.** Slice (b) must attach cuts to periods by **date containment**, reusing the `BudgetExecutionSummaryQuery` `active_period` technique: `JOIN "Cycles" cy ON cy."BudgetId" = cr."BudgetId" JOIN "Periods" p ON p."CycleId" = cy."Id" AND cr."CutDate" BETWEEN p."StartDate" AND p."EndDate"`, with `DeletedAt IS NULL` on both. Cuts outside every period are excluded from the band (still shown in slice (a)).
- Slice (b) shape: `period_cuts` CTE → `period_avg` CTE (`AVG` per total `GROUP BY PeriodId`) → outer `AVG/MIN/MAX` over `period_avg`. Decision 4 is encoded by that two-stage aggregation, not by a flat `AVG`.
- Slice (c) reuses the `ListPeriodExecutionTotals` budgeted-revision `LEFT JOIN LATERAL` on `ValidFrom/ValidTo` and its `EntryType` net formula (`Expense + DebitNote - CreditNote`), swapping `e."PeriodId" = @PeriodId` for `e."PeriodId" = ANY(@PeriodIds)` plus `bl."Id" = ANY(@LineIds)`. Within-cycle and cross-cycle are the **same query** — mode is a presentation concern that only changes which `periodIds` the client sends.
- **Cross-cycle currency risk**: `DefaultCurrencyId` lives on `Cycle`, so two cycles may convert against different currencies. Slice (c) returns `defaultCurrencyId` per period; the client MUST render a mismatch warning and MUST NOT plot mismatched cycles on one axis.

## Testing Strategy

Project has `strict_tdd: true` — this only affects **task sequencing** at `sdd-tasks` (RED test before each implementation step); no further design detail needed here.

| Layer | What to Test | Approach |
|---|---|---|
| Unit (backend) | Band two-stage aggregation, date-containment period join, `periodCount` for 0/1 cuts, `ANY(@ids)` filtering | xUnit + Shouldly, SQLite/Postgres fixture per existing `MyBudget.Features.Tests` |
| Unit (frontend) | Store actions + error paths, `useSeriesSelection` defaults/persistence, `SeriesPicker` emits, `InsufficientDataState` threshold, `BaseChart` prop→dataset mapping (Chart.js mocked) | Vitest + `@testing-library/vue`, `vi.mock('vue-chartjs')` |
| Integration | 3 endpoints: 200 for owner/admin/operator/read-only, 403 cross-budget, empty budget shape | `MyBudget.Integration.Tests` + `WebApplicationFactory` against Postgres |
| E2E | Dashboard route loads lifetime view by default; pick series → chart updates; switch comparison mode; insufficient-data state | Playwright, full Docker stack |
| i18n | All new keys exist in EN and ES | existing `i18n/__tests__/locales.spec.ts` |

## Threat Matrix

**N/A** — no shell commands, subprocesses, VCS/PR automation, executable-file classification, or process integration. The only routing added is authenticated HTTP `GET` slices behind the existing `budget:read` policy and vue-router children under the existing `requiresAuth` guard; no new auth boundary is introduced.

## Migration / Rollout

No migration required. Fully additive: 3 read endpoints, one frontend module, one route, one tab, one dependency. Rollback is a revert of the offending chained PR.

**Chained-PR slicing (400-line review budget)** — Feature Branch Chain off `feat/dashboard`; each child PR targets the previous:

| PR | Scope | Verification |
|---|---|---|
| 1 | 3 backend slices + unit/integration tests | `dotnet test` |
| 2 | Frontend foundation: dependency, `types`, Zod, `api`, `store`, `BaseChart.vue`, `useChartTheme` | `pnpm test` + `pnpm build` |
| 3 | Lifetime widgets: `SeriesPicker`, `LifetimeTotalsChart`, `TotalsBandChart`, `InsufficientDataState` + tests | `pnpm test` |
| 4 | BudgetLine widgets: `BudgetLineSeriesChart`, `ComparisonModeSwitch`, `useSeriesSelection` cross-cycle + currency-mismatch guard + tests | `pnpm test` |
| 5 | Assembly: `DashboardView`, router, `BudgetTabs`, i18n EN/ES, responsive layout, E2E | `pnpm build` + Playwright |

## Open Questions

- [ ] None blocking. KPI summary tiles (ROADMAP `11. dashboard`) remain explicitly deferred per Decision 1 — confirm at archive that the ROADMAP entry records the deferral rather than being marked fully complete.
