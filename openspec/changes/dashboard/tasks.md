# Tasks: Dashboard (Budget Analytics & Charts)

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~3000-3600 total (350-650/PR incl. tests) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | 7-PR chain off `feat/dashboard` |
| Delivery strategy | ask-on-risk (confirm) |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | PR base | Focused test | Runtime harness | Rollback |
|---|---|---|---|---|---|
| 1 | Slice (a) LifetimeCutTotals (DASH-1) | PR1←`feat/dashboard` | `dotnet test --filter LifetimeCutTotals` | `dotnet test` vs Postgres | Delete slice folder |
| 2 | Slice (b) CutTotalsBand (DASH-2,3,11) | PR2←PR1 | `dotnet test --filter CutTotalsBand` | same | Delete slice folder |
| 3 | Slice (c) BudgetLineSeries (DASH-4,5,6,12) | PR3←PR2 | `dotnet test --filter BudgetLineSeries` | same | Delete slice folder |
| 4 | FE foundation: deps/types/api/store/BaseChart | PR4←PR3 | `pnpm vitest run dashboard` | N/A, mocked Chart.js | Revert foundation files + `package.json` |
| 5 | Lifetime widgets (DASH-2,3,7,9) | PR5←PR4 | `pnpm vitest run dashboard` | N/A, component tests | Revert widget files |
| 6 | BudgetLine widgets + currency guard (DASH-4,5,6,12) | PR6←PR5 | `pnpm vitest run dashboard` | N/A, component tests | Revert widget files |
| 7 | Assembly, i18n, E2E (DASH-7,8,10) | PR7←PR6→main | `pnpm build && playwright test dashboard` | Playwright, Docker stack | Revert router/tabs/i18n/view |

## Phase 1 (PR1): Backend — Lifetime Totals (DASH-1)

- [x] 1.1 RED→GREEN: `GetLifetimeCutTotals/{Query,Handler,Endpoint}.cs` — N cuts ordered by `CutDate`; empty budget → empty series
- [x] 1.2 Integration: role scaffold — owner/admin/operator/read-only 200, no-role 403 (DASH-8)

## Phase 2 (PR2): Backend — Totals Band (DASH-2,3,11)

- [x] 2.1 RED→GREEN: `GetCutTotalsBand/{Query,Handler,Endpoint}.cs` — two-stage period-avg then MIN/MAX/AVG
- [x] 2.2 RED→GREEN: date-containment join excludes out-of-range cuts from band only; kept in lifetime series (DASH-11)
- [x] 2.3 RED→GREEN: `periodCount` 0/1 → insufficient-data flag (DASH-3)

## Phase 3 (PR3): Backend — BudgetLine Series (DASH-4,5,6,12)

- [x] 3.1 RED→GREEN: `GetBudgetLineSeries/{Query,Handler,Endpoint}.cs` — cross-cycle match by `BudgetLineId`, `ANY(@lineIds/@periodIds)`
- [x] 3.2 RED→GREEN: response includes `defaultCurrencyId` per period (DASH-12 contract)
- [x] 3.3 Integration: extend role matrix to all 3 endpoints (DASH-8)

## Phase 4 (PR4): Frontend Foundation

- [x] 4.1 Add `chart.js` + `vue-chartjs` to `package.json`
- [x] 4.2 `types/dashboard.ts` + Zod schemas for 3 DTOs incl. `conversionBasis`, `defaultCurrencyId`
- [x] 4.3 `api/dashboardApi.ts` — 3 GET calls, `base(budgetId)` helper
- [x] 4.4 RED→GREEN: `useDashboardStore` actions, per-request loading/error
- [x] 4.5 RED→GREEN: `BaseChart.vue` prop→dataset mapping, mandatory `conversionBasis` caption (DASH-9)
- [x] 4.6 `useChartTheme` composable — DaisyUI vars → Chart.js colors

## Phase 5 (PR5): Lifetime Widgets (DASH-2,3,7,9)

- [x] 5.1 RED→GREEN: `SeriesPicker.vue` — 16 series listed, emits selection
- [x] 5.2 RED→GREEN: `useSeriesSelection` — default preselect, `localStorage` persistence
- [x] 5.3 RED→GREEN: `LifetimeTotalsChart.vue` — DASH-1 data, "cut-frozen rate" label (DASH-9)
- [x] 5.4 RED→GREEN: `TotalsBandChart.vue` — band render
- [x] 5.5 RED→GREEN: `InsufficientDataState.vue` — renders when `periodCount < 2` (DASH-3)

## Phase 6 (PR6): BudgetLine Widgets (DASH-4,5,6,12)

- [ ] 6.1 RED→GREEN: `BudgetLineSeriesChart.vue` — "transaction-time rate" label (DASH-9)
- [ ] 6.2 RED→GREEN: `ComparisonModeSwitch.vue` — within-cycle vs cross-cycle period resolution
- [ ] 6.3 RED→GREEN: currency-mismatch guard — differing `defaultCurrencyId` warns, never one blended-axis chart (DASH-12)

## Phase 7 (PR7): Assembly, i18n, E2E (DASH-7,8,10)

- [ ] 7.1 `DashboardView.vue` — lifetime view default on load (DASH-7)
- [ ] 7.2 `router/index.ts` — lazy route `budgets/:budgetId/dashboard`
- [ ] 7.3 `BudgetTabs.vue` — add Dashboard tab
- [ ] 7.4 `i18n/locales/{en,es}.json` — `dashboard.*` keys; extend `locales.spec.ts` (DASH-10)
- [ ] 7.5 Responsive layout: mobile-first single column, `lg:grid-cols-12`
- [ ] 7.6 E2E: default load, series-picker updates chart, mode switch, insufficient-data state
- [ ] 7.7 E2E: role matrix — 4 roles view, no-role denied (DASH-8)
- [ ] 7.8 E2E: cycle-vs-cycle mismatched currency warns, no blended chart (DASH-12)
- [ ] 7.9 Update `openspec/ROADMAP.md` — fuse `11. dashboard` + `extended-charts`
