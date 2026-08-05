# Spec: Dashboard (Budget Analytics & Trend Views)

## Purpose

Defines the behavioral requirements for the `dashboard` capability: read-only trend and comparison views over already-persisted `CutRecord` totals and `ExecutionRecord`/`BudgetLine` data. No new writes, no schema changes.

---

## Capability: dashboard

### DASH-1: Lifetime CutRecord Totals Series

The system MUST provide a read endpoint returning, for a given BudgetId, every persisted `CutRecord`'s 16 totals (8 concepts × primary/alt), ordered by `CutDate` ascending, across all cycles/periods. Each point MUST carry `CutDate`, concept, primary value, alt value. The query MUST be read-only (Dapper), with no write path.

#### Scenario: Full lifetime series returned

- GIVEN a budget with N `CutRecord`s across 2+ cycles
- WHEN the lifetime totals endpoint is called
- THEN all N rows are returned ordered by `CutDate` ascending, each with 16 total values

#### Scenario: No cuts yet

- GIVEN a budget with zero `CutRecord`s
- WHEN the lifetime totals endpoint is called
- THEN 200 OK is returned with an empty series (not an error)

---

### DASH-2: Lifetime Average Band (Period-Averaged Min/Max)

The system MUST compute, per concept (primary/alt), an average/deviation band by first grouping `CutRecord` totals by `PeriodId`, averaging each concept within each period, and THEN computing AVG/MIN/MAX of those per-period averages across periods. The system MUST NOT compute a flat average across individual cuts directly.

#### Scenario: Band computed via period averaging

- GIVEN Period A has cuts totaling [100, 200] (period avg 150) and Period B has one cut totaling 300 (period avg 300)
- WHEN the average-band endpoint is called
- THEN AVG=225, MIN=150, MAX=300 are returned (derived from the two per-period averages, not the flat set [100, 200, 300])

---

### DASH-3: Insufficient History Empty State

WHEN a budget has 0 or 1 `CutRecord`, the system MUST render an explicit "insufficient history" empty state for the average/band widget instead of a computed band or a hidden widget.

#### Scenario: One cut only

- GIVEN a budget with exactly 1 `CutRecord`
- WHEN the dashboard requests the average band
- THEN the widget renders an explicit "not enough history" empty state, not a single-point band

---

### DASH-4: BudgetLine Per-Period Series (Cross-Cycle by BudgetLineId)

The system MUST provide a read endpoint returning `ExecutionRecord`/`BudgetLine` totals per `PeriodId` for one or more selected `BudgetLineId`s within a cycle. Cross-cycle queries MUST match rows by `BudgetLineId` — the same `BudgetLine` row, scoped to `BudgetId`, persists across cycles via its `Revisions` collection — no name/category fuzzy matching. Because identity is stable, there MUST be no "unmatched line" state.

#### Scenario: Cross-cycle series by identity

- GIVEN `BudgetLine` L exists across Cycle 1 and Cycle 2
- WHEN the per-period series is requested for L in cross-cycle mode
- THEN periods from both cycles are returned as one continuous series keyed by L's `BudgetLineId`

---

### DASH-5: Period-vs-Period Comparison (within a cycle)

The system MUST allow comparing 2+ periods within the same cycle for one or more selected `BudgetLineId`s, returning budgeted/registered values per period.

#### Scenario: Two periods compared

- GIVEN Period 1 and Period 2 of the same cycle both have `ExecutionRecord`s for `BudgetLine` L
- WHEN period-vs-period comparison is requested for L across Period 1 and Period 2
- THEN both periods' budgeted/registered values are returned side by side

---

### DASH-6: Cycle-vs-Cycle Comparison

The system MUST allow comparing aggregated `BudgetLine` totals across 2+ cycles, matched by `BudgetLineId` per DASH-4.

#### Scenario: Two cycles compared

- GIVEN `BudgetLine` L has data in Cycle 1 and Cycle 2
- WHEN cycle-vs-cycle comparison is requested for L
- THEN each cycle's aggregated totals for L are returned as separate series

---

### DASH-7: Dashboard Default View & Series Picker

The default landing view of `/budgets/:budgetId/dashboard` MUST be the lifetime trend/analysis view (DASH-1/DASH-2), not last-cut KPI tiles. The 16 `CutRecord` total series MUST be presented through a series-picker widget where the user selects which concepts to plot; the system MUST NOT render one chart forcing all 16 lines, and MUST NOT force 8 separate small-multiple charts. The view MUST remain usable at mobile viewport widths.

#### Scenario: Default view on navigation

- GIVEN a user with budget access navigates to `/budgets/:budgetId/dashboard`
- WHEN the page loads
- THEN the lifetime trend view is shown by default, with a series picker for the 16 totals

#### Scenario: Series picker selection

- GIVEN the series picker lists 16 available series, none selected
- WHEN the user selects 3 concepts
- THEN only those 3 series render on the chart; the rest stay unplotted until selected

---

### DASH-8: Role-Based Access

All 4 budget roles (owner, admin, operator, read-only) with access to a budget MUST be able to view the dashboard and every widget in it (read-only capability, no write actions exist). Users with no role on the target budget MUST be denied.

| Role | Dashboard access |
|---|---|
| owner | View |
| admin | View |
| operator | View |
| read-only | View |
| no role on budget | 403 Forbidden |

#### Scenario: Read-only role views dashboard

- GIVEN a user with `read-only` role on Budget B
- WHEN GET is called on any dashboard query endpoint for Budget B
- THEN 200 OK is returned with data

#### Scenario: Cross-budget access denied

- GIVEN a user with no role on Budget B
- WHEN GET is called on any dashboard query endpoint for Budget B
- THEN 403 Forbidden is returned

---

### DASH-9: Currency Conversion Basis Labeling

Every dashboard widget MUST display which conversion basis its values use: "cut-frozen rate" (`CutRecord`'s own `ExchangeRate`, frozen at cut time — DASH-1/DASH-2 widgets) or "transaction-time rate" (`ExecutionRecord`'s rate at registration — DASH-4/DASH-5/DASH-6 widgets). A single chart MUST NOT blend values computed under both bases.

#### Scenario: Basis label rendered

- GIVEN the lifetime totals chart (`CutRecord`-sourced)
- WHEN the widget renders
- THEN it displays a label identifying the "cut-frozen rate" basis

#### Scenario: No mixed-basis chart

- GIVEN a `BudgetLine` series (`ExecutionRecord`-sourced) and a `CutRecord` total series
- WHEN composing dashboard widgets
- THEN the two series are never plotted on the same chart/axis

---

### DASH-10: i18n Coverage

All new dashboard UI strings (widget titles, series-picker labels, empty states, basis labels, axis labels) MUST exist in both `es` and `en` locale files via vue-i18n. No hardcoded UI strings are permitted.

#### Scenario: Empty-state string localized

- GIVEN the active locale is `es`
- WHEN the insufficient-history empty state (DASH-3) renders
- THEN its text is read from the `es` locale bundle

---

### DASH-11: Period-Containment Exclusion for the Average Band

`CutRecord` has no `PeriodId` FK — it only carries `BudgetId` and `CutDate`. The average/band query (DASH-2) MUST attach each `CutRecord` to a `Period` by date containment (`CutDate BETWEEN Period.StartDate AND Period.EndDate`, scoped through the cut's `Cycle`), reusing the `active_period` join technique from `BudgetExecutionSummaryQuery`. Any `CutRecord` whose `CutDate` does not fall within any `Period`'s date range MUST be excluded from the period-grouped average/band computation (DASH-2). This exclusion MUST NOT apply to the lifetime totals series (DASH-1), which is not period-grouped and MUST still include the cut.

#### Scenario: Cut outside all period ranges excluded from the band only

- GIVEN a Budget has a `CutRecord` dated outside every `Period`'s date range
- WHEN the average-band endpoint (DASH-2) and the lifetime totals endpoint (DASH-1) are both called
- THEN the band chart's data points do not include that cut, while the lifetime series chart still includes it

---

### DASH-12: Cross-Cycle Currency Mismatch Guard

`DefaultCurrencyId` lives on `Cycle`, not `Budget`, so a period-vs-period or cycle-vs-cycle comparison (DASH-5/DASH-6) MAY involve two `Cycle`s with different `DefaultCurrencyId` values. The per-period series query MUST return `defaultCurrencyId` per period alongside the series data. WHEN the client detects that two periods being compared/plotted together have different `defaultCurrencyId` values, it MUST warn the user and MUST NOT silently render a single chart blending both currencies on one shared axis.

#### Scenario: Cycle-vs-cycle comparison with mismatched currencies

- GIVEN Cycle 1 has `DefaultCurrencyId` = USD and Cycle 2 has `DefaultCurrencyId` = EUR
- WHEN the user requests a cycle-vs-cycle comparison (DASH-6) between Cycle 1 and Cycle 2
- THEN the system does not render a single blended-currency chart, and the user is warned of the mismatch

---

## Non-Goals (Explicit Exclusions)

- No export of any dashboard chart (PDF/CSV/image).
- No `projects` / `commitments` / `installments` / savings-goal analytics — those entities do not exist yet.
- No write path anywhere in the dashboard module; strictly read-only.
- No database schema or migration changes.
