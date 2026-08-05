# Proposal: Dashboard (Budget Analytics & Charts)

## Intent

The owner can inspect one cut (`current-situation`) or one period (`budget-execution`), but cannot see behavior over time. The history already exists — every `CutRecord` persists 16 frozen totals (8 concepts x primary/alt), every `ExecutionRecord` carries a `PeriodId` — yet none of it is readable as a trend. This change turns that dormant history into the budget's analytical surface, superseding ROADMAP `11. dashboard` (MVP A) and `extended-charts` (MVP B) as one fused change.

## Scope

### In Scope

- **Lifetime totals series**: every `CutRecord` totals row over `CutDate`, all cycles/periods, 16 series (primary + alt).
- **Lifetime average band**: period AVG of those totals with MIN/MAX deviation band.
- **BudgetLine charts** (multi-select): across periods of one cycle; period-vs-period within a cycle; cycle-vs-cycle.
- **Dashboard module**: new Vue VSA feature `features/dashboard/`, composable chart widgets, responsive views under `budgets/:budgetId`, role-gated (read-only MAY view).
- 3 new Dapper read slices (no schema change), ES/EN i18n keys, chart library dependency.

### Out of Scope

- Any migration or entity change; any write path from the dashboard.
- Chart export (PDF/CSV/image).
- `projects` / `commitments` / `installments` analytics; savings-goal progress (no goal entity).

## Capabilities

### New Capabilities

- `dashboard`: lifetime totals and average-band series, BudgetLine per-period and cross-cycle series, dashboard views/widgets, role gating, i18n.

### Modified Capabilities

- None. `current-situation` and `budget-execution` requirements are unchanged; the dashboard only reads their persisted data.

## Approach

Query layer + presentation only. Backend: 3 Dapper CTE slices following `BudgetExecutionSummaryQuery` / `ListPeriodExecutionTotalsHandler` — (a) lifetime CutRecord totals series (indexed `BudgetId`, ordered by `CutDate`), (b) AVG/MIN/MAX over the same rows, (c) per-BudgetLine per-period series with cross-cycle mode (`GROUP BY PeriodId` instead of single-period filter). Frontend: `features/dashboard/` mirroring `features/current-situation/` (`api/components/composables/store/types/views` + colocated `__tests__`); chart library chosen at design.

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `MyBudget.Features/Features/Dashboard/` | New | 3 read slices + endpoints |
| `frontend/src/features/dashboard/` | New | Module, views, chart widgets |
| `frontend/src/router/index.ts` | Modified | Routes under `budgets/:budgetId` |
| `frontend/package.json` | Modified | Chart library |
| `frontend/src/locales/{en,es}` | Modified | Dashboard i18n keys |
| `openspec/ROADMAP.md` | Modified | Fuse both entries |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| 6+ chart types blow the 400-line PR budget | High | Feature-branch chain, slice per query / chart family; forecast at `sdd-tasks` |
| `CutRecord` freezes its own rate per cut, `ExecutionRecord` uses transaction-time rate | Med | Spec MUST forbid mixing both sources in one chart; label conversion basis |
| Cross-cycle BudgetLine identity (dates/category differ) | Med | Spec MUST define matching rule + "unmatched line" state |
| Chart library lock-in / bundle weight | Med | Decide at design with tradeoff record; wrap behind thin component |
| Sparse history (0-2 cuts) makes bands meaningless | Med | Spec empty / insufficient-data states |

## Rollback Plan

Fully additive: no migration, no write path. Revert `feat/dashboard` (or the offending chained PR) — remove `Features/Dashboard/`, `features/dashboard/`, router entries, chart dependency, i18n keys. Schema and stored data untouched, so no data repair is needed.

## Dependencies

- Archived: `cut-record-totals-persistence` (16 totals columns), `budget-execution`, `current-situation`.
- One new frontend charting dependency (chosen at design).
- Branch `feat/dashboard` created before implementation (branch-before-cycle).

## Success Criteria

- [ ] Lifetime per-cut behavior of all 8 concepts visible in both currencies.
- [ ] Lifetime AVG band with MIN/MAX deviation visible.
- [ ] Multi-select BudgetLines compared across periods, period-vs-period, and cycle-vs-cycle.
- [ ] Views usable at mobile viewport widths.
- [ ] Every chart states its conversion basis; none mixes cut-frozen and transaction-time rates.
- [ ] Empty / insufficient-data states render for a budget with 0-1 cuts.
- [ ] Read-only role can view; cross-budget access denied.
- [ ] All new strings exist in EN and ES; no DB migration introduced.

## Open Questions (spec/design)

1. Default landing view: lifetime overview or last-cut KPIs?
2. 16 totals as one dense chart, small multiples, or user-selected series?
3. Cross-cycle BudgetLine matching key: identity, name, or category?
4. "Period average" = per-period averaging of cut totals, or plain average of all cuts?
5. Are summary KPI tiles (ROADMAP `11. dashboard`) in scope here or deferred?
