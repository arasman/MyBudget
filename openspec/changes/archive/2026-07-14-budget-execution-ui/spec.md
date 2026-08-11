# Budget Execution UI — Specification

## Purpose

Define the observable frontend behavior of the multi-period budget matrix view (`budget-execution-ui`). All requirements are frontend-only. Backend endpoints are already implemented and archived.

---

## Capabilities

| Capability | Type |
|---|---|
| `budget-execution-ui` | New — full spec |
| `budget-structure-ui` (BudgetTabs) | Delta — MODIFIED |

---

# budget-execution-ui Specification

---

## REQ-MATRIX-ROUTE — Routing and Navigation

### Requirement: Matrix Route Registration

The router MUST expose route `/budgets/:budgetId/cycles/:cycleId/matrix` mapped to `BudgetMatrixView`. Navigating to a matrix URL without an active cycle MUST redirect to `CycleListView`. The `BudgetTabs` component MUST render a "Matrix" tab only when a `cycleId` prop is provided; the tab MUST be active when the current route matches `BudgetMatrix`.

#### Scenario: Navigate to matrix URL directly

- GIVEN a budget with an active cycle exists
- WHEN the user navigates to `/budgets/:budgetId/cycles/:cycleId/matrix`
- THEN `BudgetMatrixView` renders and begins loading matrix data

#### Scenario: Navigate to matrix without cycleId

- GIVEN the user is on any budget route with no cycle selected
- WHEN the user attempts to navigate to the matrix route (no `:cycleId`)
- THEN the router redirects to `CycleListView` for that budget

#### Scenario: Matrix tab visible with cycleId

- GIVEN the user is on `CycleDetailView` (which provides `cycleId` to `BudgetTabs`)
- WHEN `BudgetTabs` renders
- THEN a "Matrix" tab is visible and links to the `BudgetMatrix` route

#### Scenario: Matrix tab hidden without cycleId

- GIVEN the user is on `BudgetSelectionView` (no `cycleId` available)
- WHEN `BudgetTabs` renders
- THEN the "Matrix" tab is NOT rendered

---

## REQ-MATRIX-NAV — Matrix Navigation and Layout

### Requirement: Three-Period Sliding Window

The matrix MUST display exactly 3 period columns simultaneously. On mount, the matrix MUST load execution totals for the 3 currently visible periods. Navigating to the previous or next period MUST shift the window by one period. The "Previous period" button MUST be disabled when the first period is already visible. The "Next period" button MUST be disabled when the last period is already visible.

#### Scenario: Initial load shows 3 periods

- GIVEN a cycle with 5 periods
- WHEN `BudgetMatrixView` mounts
- THEN periods 1, 2, 3 are rendered as columns with progressive skeleton loaders
- AND totals for each column load independently

#### Scenario: Navigate to next period

- GIVEN periods 1, 2, 3 are visible
- WHEN the user clicks "Next period"
- THEN periods 2, 3, 4 are visible; period 4 fetches its totals
- AND the "Previous period" button is enabled

#### Scenario: Disabled at last period

- GIVEN the last 3 periods (3, 4, 5) are visible in a 5-period cycle
- WHEN the view renders
- THEN the "Next period" button is disabled

#### Scenario: Disabled at first period

- GIVEN the first 3 periods (1, 2, 3) are visible
- WHEN the view renders
- THEN the "Previous period" button is disabled

### Requirement: Period Column Header

Each visible period column MUST display a header containing: period name/number, date range (start date — end date), and two sub-column labels (Presupuesto / Ejecutado). The sticky left column MUST remain fixed during horizontal scroll.

#### Scenario: Period header renders

- GIVEN period 2 spans "2026-02-01" to "2026-02-28"
- WHEN the matrix renders
- THEN the column header shows "Período 2", "Feb 1 – Feb 28", and sub-labels "Presupuesto" / "Ejecutado"

#### Scenario: Sticky left column stays fixed

- GIVEN the matrix has many period columns wider than the viewport
- WHEN the user scrolls horizontally
- THEN the row labels column remains anchored at the left edge

---

## REQ-MATRIX-STRUCT — Hierarchical Structure Display

### Requirement: Row Hierarchy Rendering

The matrix MUST render rows in order: CategoryGroup → Category → BudgetLine → EstimatedVariance sub-row. CategoryGroup rows MUST show name, collapse/expand control, up/down arrows, and aggregated totals per visible period. Category rows MUST show name, collapse/expand control, up/down arrows, and aggregated totals per visible period. BudgetLine rows MUST show name, up/down arrows, and two values per period: Real (latest `BudgetLineRevision.BudgetedAmount`) and Ejecutado (net executed). The EstimatedVariance sub-row MUST show: `Estimado - Real` and `Real - Total Ejecutado`.

#### Scenario: Group with categories and lines renders

- GIVEN a group "Vivienda" contains category "Alquiler" containing line "Renta"
- WHEN the matrix renders
- THEN the hierarchy renders: Vivienda → Alquiler → Renta → EstimatedVariance sub-row

#### Scenario: Collapse group hides children

- GIVEN a CategoryGroup is expanded
- WHEN the user clicks its collapse control
- THEN all Category and BudgetLine rows under that group are hidden
- AND the group's totals row remains visible

#### Scenario: Empty state when no groups exist

- GIVEN a cycle with no CategoryGroups
- WHEN the matrix renders
- THEN an `EmptyState` component appears with a link to navigate to the Categories view

---

## REQ-MATRIX-EXEC — Execution Record Management

### Requirement: ExecutionListModal Trigger

Double-clicking on an Ejecutado cell in a BudgetLine row MUST open `ExecutionListModal` for that line × period combination. The modal MUST list execution records ordered by `CreatedAt` ascending. When the period is closed, the create form MUST be hidden and all records MUST be read-only. When the period is open, the form MUST be visible.

#### Scenario: Double-click opens modal

- GIVEN a BudgetLine row is visible and the period is open
- WHEN the user double-clicks the Ejecutado cell for that line × period
- THEN `ExecutionListModal` opens displaying existing records for that line and period

#### Scenario: Closed period hides form

- GIVEN a period is closed
- WHEN the user double-clicks an Ejecutado cell in that period column
- THEN `ExecutionListModal` opens but shows no create/edit form
- AND all listed records are read-only

### Requirement: Execution Record CRUD

When the period is open, a budget:operator member MUST be able to create, update, and soft-delete execution records. `EntryType` MUST be one of: Expense, CreditNote, DebitNote. `Amount` MUST be positive. `Note` MUST be required when `EntryType` is CreditNote or DebitNote. After any create, update, delete, or restore, the Ejecutado cell and period totals for that column MUST refresh.

#### Scenario: Create execution record

- GIVEN `ExecutionListModal` is open for an open period
- WHEN the user fills in Type=Expense, Amount=500, Note=optional and submits
- THEN the record appears in the list and the Ejecutado amount for that cell increases

#### Scenario: Note required for CreditNote

- GIVEN the user selects EntryType=CreditNote and leaves Note empty
- WHEN the user submits the form
- THEN a validation error "Note is required for Credit Note and Debit Note" is shown
- AND the record is NOT created

#### Scenario: Delete execution record

- GIVEN an execution record exists and the period is open
- WHEN the user clicks delete and confirms
- THEN the record is soft-deleted (disappears from the list when showDeleted=false)
- AND the Ejecutado total for that period column updates

#### Scenario: Restore deleted record

- GIVEN "Incluir eliminados" is checked and a deleted record is visible
- WHEN the user clicks restore on a deleted record
- THEN the record becomes active and the Ejecutado total updates

---

## REQ-MATRIX-TOTALS — Totals and Calculations

### Requirement: Aggregated Totals

BudgetLine MUST show Real = latest `BudgetLineRevision.BudgetedAmount` and Ejecutado = net executed amount from API (`Σ Expense + Σ DebitNote − Σ CreditNote`). Category row totals MUST equal the sum of its BudgetLine Reals and Ejecutados. CategoryGroup row totals MUST equal the sum of its Category totals. Three bottom summary rows (by LineType) MUST show: Total Estimado | Total Real | Total Ejecutado per visible period. Expense summary MUST use red styling; LongTermSavings green; PreventiveSavings orange.

#### Scenario: Category totals aggregate lines

- GIVEN category "Alquiler" has lines with Real=1000 and Real=500 for period 1
- WHEN the matrix renders
- THEN the "Alquiler" row shows Total Real=1500 for period 1

#### Scenario: Summary row shows expense total

- GIVEN period 1 has expense lines with Ejecutado=800 and Ejecutado=200
- WHEN the matrix renders
- THEN the "Total de Gasto" summary row shows Total Ejecutado=1000 for period 1 in red styling

#### Scenario: Totals refresh after CRUD

- GIVEN a user deletes an execution record of 100 for a line in period 2
- WHEN the delete succeeds
- THEN the BudgetLine Ejecutado cell, Category total, Group total, and summary row all update for period 2

---

## REQ-MATRIX-CURRENCY — Currency Display

### Requirement: Currency Toggle

The matrix MUST default to displaying amounts in the cycle's default currency (GTQ). A currency toggle (GTQ / USD) MUST be visible in `MatrixControls`. The alternate currency option MUST only be enabled when `Cycle.AlternateCurrencyId` and `Cycle.ExchangeRate` are set. When toggled to alternate currency, ALL amount cells MUST be converted client-side using `amount_usd = amount_gtq / exchangeRate`. The exchange rate MUST be displayed in the controls header. Toggling back to default MUST restore original amounts without precision loss.

#### Scenario: Default currency on load

- GIVEN a cycle with GTQ as default
- WHEN the matrix loads
- THEN all amounts display in GTQ and the GTQ toggle is selected

#### Scenario: Toggle to USD converts amounts

- GIVEN a cycle with ExchangeRate=7.5 and AlternateCurrencyId set
- WHEN the user selects USD in the currency toggle
- THEN all amount cells convert: amount_usd = amount_gtq / 7.5
- AND the exchange rate "7.5 GTQ per 1 USD" is displayed in the header

#### Scenario: Alternate currency toggle disabled without exchange rate

- GIVEN a cycle with no `AlternateCurrencyId` or `ExchangeRate`
- WHEN the matrix renders
- THEN the USD toggle option is disabled

#### Scenario: Toggle back to GTQ restores amounts

- GIVEN the user has toggled to USD
- WHEN the user selects GTQ
- THEN amounts return to their original GTQ values from the store (no rounding loss)

---

## REQ-MATRIX-DELETED — Include Deleted Behavior

### Requirement: Show/Hide Deleted Items

An "Incluir eliminados" checkbox MUST be present in `MatrixControls`, unchecked by default. When checked, soft-deleted CategoryGroups, Categories, BudgetLines, and ExecutionRecords MUST become visible. Deleted items MUST render in gray and MUST be read-only — no edit, no delete, only restore. When unchecked, deleted items MUST be hidden and the matrix MUST re-fetch data for all visible periods.

#### Scenario: Check "Incluir eliminados" shows deleted group

- GIVEN a CategoryGroup has been soft-deleted
- WHEN the user checks "Incluir eliminados"
- THEN the deleted group appears in gray with no edit controls, only a restore action

#### Scenario: Uncheck hides deleted items

- GIVEN "Incluir eliminados" is checked and deleted items are visible
- WHEN the user unchecks the checkbox
- THEN deleted items disappear and the store re-fetches execution totals for all visible periods

#### Scenario: Deleted execution records in modal

- GIVEN "Incluir eliminados" is checked and a deleted execution record exists
- WHEN the user opens `ExecutionListModal` for the corresponding cell
- THEN the deleted record appears in gray with only a restore action

---

## REQ-MATRIX-INSERT — Structural Inserts from Matrix

### Requirement: Insert BudgetLine from Matrix

Each category section MUST display an "Insertar Línea" link below its last BudgetLine row. Clicking it MUST open the BudgetLine create modal (reusing `BudgetLineModal.vue`). A newly created line MUST appear in ALL visible period columns with Ejecutado=0 and Real=0 for periods without a revision.

#### Scenario: Insert line appears in all periods

- GIVEN a category "Alquiler" with 2 existing lines
- WHEN the user clicks "Insertar Línea" and creates a new line
- THEN the new line appears as a row with Real=0 and Ejecutado=0 for each visible period

### Requirement: Insert Category and Group from Matrix

A "Insertar Categoría" link MUST appear at the bottom of each CategoryGroup section. An "Insertar Grupo" link MUST appear below all groups. Both MUST open the existing `CategoryForm.vue` and `CategoryGroupForm.vue` modals respectively. After creation, the matrix MUST re-render to include the new row.

#### Scenario: Insert category opens CategoryForm

- GIVEN the user clicks "Insertar Categoría" in a group
- WHEN `CategoryForm` modal opens and the user submits
- THEN a new Category row appears under that group in the matrix

---

## REQ-MATRIX-REORDER — Reordering

### Requirement: BudgetLine Reorder

BudgetLine rows MUST support reordering via up/down arrow buttons AND drag-and-drop within their parent Category. Both mechanisms MUST call the `ReorderBudgetLines` endpoint. The draggable behavior MUST use `vue-draggable-plus`. Up arrow MUST be disabled on the first line; down arrow MUST be disabled on the last line.

#### Scenario: Up arrow moves line

- GIVEN a BudgetLine is at position 2 in its category
- WHEN the user clicks the up arrow
- THEN the line moves to position 1 and the reorder endpoint is called

#### Scenario: Drag line within category

- GIVEN a BudgetLine is dragged to a new position within its category
- WHEN the drag ends
- THEN the line's display order updates and `ReorderBudgetLines` is called

### Requirement: CategoryGroup and Category Reorder

CategoryGroup rows MUST support up/down arrows and drag-and-drop calling `ReorderCategoryGroups`. Category rows MUST support up/down arrows and drag-and-drop within their parent group calling `ReorderCategories`.

#### Scenario: Drag group to new position

- GIVEN two CategoryGroups exist
- WHEN the user drags the second group above the first
- THEN `ReorderCategoryGroups` is called and the order updates

---

## REQ-MATRIX-REFRESH — Per-Period Refresh

### Requirement: Period Column Refresh

A refresh icon MUST be visible per period column when the period is closed. Clicking the icon MUST re-fetch execution totals for that single period via `GET /periods/:id/execution-totals`. A loading indicator MUST be shown in the column during the fetch.

#### Scenario: Refresh closed period column

- GIVEN period 2 is closed and its column shows a refresh icon
- WHEN the user clicks the refresh icon
- THEN a loading skeleton appears in that column and execution totals reload
- AND amounts update when the response arrives

#### Scenario: No refresh icon on open period

- GIVEN period 3 is open
- WHEN the matrix renders
- THEN no refresh icon appears in the period 3 column header

---

## REQ-MATRIX-RBAC — Access Control

### Requirement: Role-Based UI Visibility

Members with `budget:read` role MUST see the matrix but MUST NOT see create/edit/delete controls for execution records or structural elements. Members with `budget:operator` role MUST be able to create, update, and delete execution records. Members with `budget:admin` role MUST additionally be able to insert and reorder lines, categories, and groups. Non-members receiving a 403 MUST be redirected away from the matrix route.

#### Scenario: Read-only member sees no CRUD controls

- GIVEN a user with `budget:read` role
- WHEN the matrix renders and they open `ExecutionListModal`
- THEN no "Add Entry", edit, or delete buttons are visible

#### Scenario: Operator can create execution records

- GIVEN a user with `budget:operator` role and an open period
- WHEN they open `ExecutionListModal`
- THEN the "Add Entry" form is visible and submission succeeds

#### Scenario: Admin can insert lines and reorder

- GIVEN a user with `budget:admin` role
- WHEN the matrix renders
- THEN "Insertar Línea", "Insertar Categoría", "Insertar Grupo" links and reorder arrows are visible

#### Scenario: Non-member redirected

- GIVEN a user with no membership in the budget
- WHEN they navigate to the matrix route
- THEN the API returns 403 and the UI redirects to a safe route (e.g., BudgetSelectionView)

---

# Delta: budget-structure-ui — BudgetTabs

## MODIFIED Requirements

### Requirement: BudgetTabs Navigation

`BudgetTabs.vue` MUST render tab items: "Cycles", "Categories", and — when a `cycleId` prop is provided — "Matrix". The "Matrix" tab MUST link to the `BudgetMatrix` named route using `{ budgetId, cycleId }` params. The "Matrix" tab MUST NOT render when `cycleId` is absent or undefined.
(Previously: BudgetTabs rendered only "Cycles" and "Categories" tabs with no cycleId awareness.)

#### Scenario: Matrix tab renders with cycleId

- GIVEN `BudgetTabs` receives `cycleId="abc-123"`
- WHEN it renders
- THEN three tabs are visible: "Cycles", "Categories", "Matrix"

#### Scenario: Matrix tab absent without cycleId

- GIVEN `BudgetTabs` receives no `cycleId` prop
- WHEN it renders
- THEN only "Cycles" and "Categories" tabs are visible

#### Scenario: Matrix tab active on matrix route

- GIVEN the current route is `BudgetMatrix`
- WHEN `BudgetTabs` renders
- THEN the "Matrix" tab has the active CSS class

---

## i18n Requirements

### Requirement: i18n Namespaces

`en.json` and `es.json` MUST include `budgetMatrix.*` and `budgetExecution.*` key namespaces covering all matrix labels, navigation, column headers, summary rows, empty states, validation messages, and execution record form labels. All user-visible text in the matrix feature MUST reference i18n keys (no hardcoded strings).

#### Scenario: All matrix text is localized

- GIVEN the app locale is switched from EN to ES
- WHEN the matrix view renders
- THEN all labels, column headers, modal titles, and button text render in Spanish

---

## Test Coverage Requirements

### Requirement: Unit and Component Tests

Vitest tests MUST cover: `useBudgetMatrixStore` actions (initMatrix, navigatePrev/Next, currency toggle, showDeleted toggle), `useMatrixNavigation` (sliding window edge clamping), `useCurrencyDisplay` (GTQ→USD conversion), `MatrixCell.vue` (dblclick emit), `ExecutionRecordForm.vue` (note validation), `ExecutionListModal.vue` (closed period → form hidden), `MatrixSummaryRow.vue` (totals and color classes).

### Requirement: Playwright E2E Specs

Eight Playwright specs MUST cover: matrix navigation (3 periods, prev/next, edges), collapse/expand, execution CRUD (double-click → modal → create → total updates), currency toggle, include-deleted toggle, closed period read-only, RBAC (operator vs non-member).
