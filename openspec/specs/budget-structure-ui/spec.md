# budget-structure-ui Specification

## Purpose

Defines the full CRUD UI for budget structure entities: Cycles, Periods, CategoryGroups, Categories, and BudgetLines. Covers navigation model, role gating, empty states, drag-and-drop reorder, and i18n.

---

## Requirements

### Requirement: REQ-NAV-1 — Budget Structure Navigation Tabs

The budget detail view MUST render three tabs when a `cycleId` prop is provided: "Cycles", "Categories", and "Matrix". When no `cycleId` prop is provided, only "Cycles" and "Categories" tabs are rendered. The "Matrix" tab MUST link to the `BudgetMatrix` named route using `{ budgetId, cycleId }` params. The "Matrix" tab MUST have its own active state tracking (not grouped with CYCLE_ROUTE_NAMES). Navigating between tabs MUST NOT lose the active budget context.

When the current user's role for the active budget is `owner` or `admin` (`useRoleGate(budgetId).isAdmin`), a "Members" tab MUST also be rendered, placed after "Dashboard" as the LAST tab in the bar — "Dashboard"'s own existing position MUST NOT change, so non-admin users see zero difference in their tab bar — following the same `RouterLink` + `isActive()` pattern as the existing tabs, and linking to the `BudgetMembers` named route under `/budgets/:budgetId/members`. When the current user's role is `operator` or `read-only`, the "Members" tab MUST NOT be rendered at all — entirely absent from the DOM, not merely disabled.

(Previously: `BudgetTabs` rendered "Cycles" / "Categories" / optionally "Matrix" with no member-role-based tab gating.)

#### Scenario: Default tab on budget entry

- GIVEN the user navigates to `/budgets/:budgetId`
- WHEN the page renders
- THEN the "Cycles" tab is active by default

#### Scenario: Tab switch updates URL

- GIVEN the user is on the Cycles tab
- WHEN they click the "Categories" tab
- THEN the URL reflects the Categories tab route

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

#### Scenario: Members tab visible to Owner (WU1)

- GIVEN the caller has `owner` role for the active budget
- WHEN `BudgetTabs` renders
- THEN a "Members" tab is visible as the last tab, after "Dashboard"
- AND "Dashboard" remains at its existing position (not moved)

#### Scenario: Members tab visible to Admin (WU1)

- GIVEN the caller has `admin` role for the active budget
- WHEN `BudgetTabs` renders
- THEN a "Members" tab is visible

#### Scenario: Members tab hidden from Operator (WU1)

- GIVEN the caller has `operator` role for the active budget
- WHEN `BudgetTabs` renders
- THEN no "Members" tab element exists in the DOM

#### Scenario: Members tab hidden from ReadOnly (WU1)

- GIVEN the caller has `read-only` role for the active budget
- WHEN `BudgetTabs` renders
- THEN no "Members" tab element exists in the DOM

#### Scenario: Members tab active state (WU1)

- GIVEN the current route is `BudgetMembers`
- WHEN `BudgetTabs` renders
- THEN the "Members" tab has the active CSS class

---

### Requirement: REQ-CYC-1 — Cycle List

The system MUST display all cycles for the active budget via `GET /api/budgets/{budgetId}/cycles`. Each row MUST show name, start date, end date, period count, and active status. When a cycle has an alternate currency, the list MUST also display the alternate currency symbol or code. An empty state with a guided prompt MUST be shown when no cycles exist.

#### Scenario: Cycles listed

- GIVEN the budget has two cycles
- WHEN the Cycles tab is active
- THEN both cycles are displayed with name, date range, period count, and active badge

#### Scenario: Empty state shown

- GIVEN the budget has no cycles
- WHEN the Cycles tab is active
- THEN a guided empty-state prompt is shown instead of an empty table

#### Scenario: Alternate currency shown when present

- GIVEN a cycle with alternateCurrency.code="USD"
- WHEN the Cycles tab renders
- THEN the cycle row displays the alternate currency symbol or code

#### Scenario: Alternate currency absent when not set

- GIVEN a cycle with alternateCurrency=null
- WHEN the Cycles tab renders
- THEN no alternate currency indicator is shown for that row

---

### Requirement: REQ-I18N-1 — Budget Structure i18n Keys

All user-visible strings in the budget structure UI MUST use keys under the `budgetStructure.*` namespace in `en.json` and `es.json`. No hardcoded English or Spanish strings MAY appear in template markup. The keys `budgetStructure.cycles.defaultCurrency`, `budgetStructure.cycles.alternateCurrency`, and `budgetStructure.cycles.exchangeRate` MUST be present in both locale files with appropriate translations.

Additionally, the following validation inline-error keys MUST be present in both locale files:
- `budgetStructure.categoryGroups.validation.nameRequired`
- `budgetStructure.categoryGroups.validation.nameTooLong`
- `budgetStructure.categories.validation.nameRequired`
- `budgetStructure.categories.validation.nameTooLong`
- `budgetStructure.cycles.validation.nameRequired`
- `budgetStructure.cycles.validation.nameTooLong`
- `budgetStructure.periods.validation.nameRequired`
- `budgetStructure.periods.validation.nameTooLong`
- `budgetStructure.periods.validation.startDateRequired`
- `budgetStructure.periods.validation.endDateRequired`
- `budgetStructure.periods.validation.dateOrder`
- `budgetStructure.budgetLines.validation.nameRequired`
- `budgetStructure.budgetLines.validation.nameTooLong`
- `budgetStructure.budgetLines.validation.amountRequired`
- `budgetStructure.budgetLines.validation.amountPositive`

The following error-toast keys MUST also be present in both locale files:
- `budgetStructure.selection.budgetNameDuplicate`
- `budgetStructure.categoryGroups.errors.nameDuplicate`
- `budgetStructure.categories.errors.nameDuplicate`
- `budgetStructure.cycles.errors.dateOverlap`
- `budgetStructure.cycles.errors.nameDuplicate`
- `budgetStructure.periods.errors.nameDuplicate`
- `budgetStructure.periods.errors.outOfCycleRange`
- `budgetStructure.periods.errors.dateOverlap`
- `budgetStructure.budgetLines.errors.nameDuplicate`

#### Scenario: All budget structure strings are i18n-keyed

- GIVEN the EN and ES locale files with the full key set
- WHEN all `budgetStructure.*` keys are present
- THEN rendering the budget structure UI in either locale shows fully translated text with no fallback warnings

#### Scenario: Currency i18n keys resolve in English

- GIVEN locale is "en"
- WHEN the cycle form or detail view renders currency labels
- THEN `budgetStructure.cycles.defaultCurrency`, `budgetStructure.cycles.alternateCurrency`, and `budgetStructure.cycles.exchangeRate` each resolve to a non-empty English string

#### Scenario: Currency i18n keys resolve in Spanish

- GIVEN locale is "es"
- WHEN the cycle form or detail view renders currency labels
- THEN the three currency keys each resolve to a non-empty Spanish string

#### Scenario: Validation i18n keys resolve in both locales

- GIVEN locale is "en" or "es"
- WHEN inline validation triggers in any structure form
- THEN the displayed message uses a translated string, not a hardcoded English literal

---

### Requirement: REQ-FORM-INLINE-VAL-1 — Inline Validation on Structure Forms

All six structure forms MUST perform client-side inline validation before submitting. Validation
MUST block form submission and display inline messages at the field level using i18n keys.

| Form | Field | Rules |
|---|---|---|
| CategoryGroupForm | name | required, max 200 |
| CategoryForm | name | required, max 200 |
| CycleForm | name | required, max 200 |
| PeriodForm | name | required, max 200 |
| PeriodForm | startDate | required, < endDate |
| PeriodForm | endDate | required, > startDate |
| BudgetLineModal | name | required, max 200 |
| BudgetLineModal | amount | required, > 0 |

#### Scenario: CategoryGroupForm name required `@unit`
- GIVEN an empty name field in CategoryGroupForm
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetStructure.categoryGroups.validation.nameRequired` is shown inline

#### Scenario: CategoryGroupForm name too long `@unit`
- GIVEN a name exceeding 200 characters in CategoryGroupForm
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetStructure.categoryGroups.validation.nameTooLong` is shown inline

#### Scenario: BudgetLineModal amount must be positive `@unit`
- GIVEN amount = 0 in BudgetLineModal
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetStructure.budgetLines.validation.amountPositive` is shown inline

#### Scenario: PeriodForm date order enforced `@unit`
- GIVEN startDate is after or equal to endDate in PeriodForm
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetStructure.periods.validation.dateOrder` is shown inline

---

### Requirement: REQ-WRAP-RETHROW-1 — store._wrap() Re-throws Errors

The `_wrap()` utility in `budget-structure/store.ts` MUST re-throw errors after logging or setting
`store.error`, so that callers can catch API errors via `try/catch`.

#### Scenario: API error propagates to caller `@unit`
- GIVEN a store action wrapped in `_wrap()` whose API call returns 422
- WHEN the view awaits the store action
- THEN the error is not silently swallowed — it propagates to the awaiting caller's catch block

---

### Requirement: REQ-ERROR-TOAST-1 — Error Toasts on Business Rule Violations

View action handlers for all structure entities MUST wrap store calls in `try/catch` and push
an error toast via `toastStore.push({ type: 'error', title: t(key) })` when the API returns a
business-rule error code. The mapping of API error codes to i18n keys MUST be:

| API Error Code | i18n Key |
|---|---|
| `BUDGET_NAME_DUPLICATE` | `budgetStructure.selection.budgetNameDuplicate` |
| `CATEGORY_GROUP_NAME_DUPLICATE` | `budgetStructure.categoryGroups.errors.nameDuplicate` |
| `CATEGORY_NAME_DUPLICATE` | `budgetStructure.categories.errors.nameDuplicate` |
| `CYCLE_DATE_OVERLAP` | `budgetStructure.cycles.errors.dateOverlap` |
| `CYCLE_NAME_DUPLICATE` | `budgetStructure.cycles.errors.nameDuplicate` |
| `PERIOD_NAME_DUPLICATE` | `budgetStructure.periods.errors.nameDuplicate` |
| `PERIOD_OUT_OF_CYCLE_RANGE` | `budgetStructure.periods.errors.outOfCycleRange` |
| `PERIOD_DATE_OVERLAP` | `budgetStructure.periods.errors.dateOverlap` |
| `BUDGET_LINE_NAME_DUPLICATE` | `budgetStructure.budgetLines.errors.nameDuplicate` |

#### Scenario: Duplicate category group name shows error toast `@unit`
- GIVEN the user submits CategoryGroupForm with a duplicate name
- WHEN the API returns 422 with code `CATEGORY_GROUP_NAME_DUPLICATE`
- THEN `toastStore.push({ type: 'error', title: t('budgetStructure.categoryGroups.errors.nameDuplicate') })` is called

#### Scenario: No silent failure on cycle date overlap `@unit`
- GIVEN the user submits CycleForm with overlapping dates
- WHEN the API returns 422 with code `CYCLE_DATE_OVERLAP`
- THEN an error toast is shown and no success toast is emitted

#### Scenario: No error toast on successful create `@unit`
- GIVEN the user submits any structure form successfully
- WHEN the API returns 201 or 200
- THEN no error toast is pushed

---

### Requirement: REQ-CYCLE-LIST-INLINE-VAL-1 — CycleListView Inline Edit Validation

The inline edit in `CycleListView.vue` MUST apply the same validation as `CycleForm.vue`:
name required, name max 200, startDate required, endDate required, endDate > startDate.

#### Scenario: Inline edit — empty name blocked `@unit`
- GIVEN the user clears the cycle name in the inline edit row
- WHEN they attempt to save
- THEN the save is blocked and an inline error is shown (using `budgetStructure.cycles.validation.nameRequired`)

#### Scenario: Inline edit — invalid date order blocked `@unit`
- GIVEN the user sets endDate before startDate in the inline edit row
- WHEN they attempt to save
- THEN the save is blocked and an inline error is shown (using `budgetStructure.periods.validation.dateOrder` or equivalent cycle key)

---

### Requirement: REQ-CYC-TYPES-1 — CycleListItem and CycleDetail Type Extensions

`CycleListItem` and `CycleDetail` TypeScript types MUST include optional fields `alternateCurrencyId` (string | null), `exchangeRate` (number | null), and `alternateCurrency` (CurrencyItem | null).

#### Scenario: Type accepts null alternate fields

- GIVEN an API response for a cycle without alternate currency
- WHEN the response is parsed into CycleListItem
- THEN the type accepts alternateCurrencyId=null, exchangeRate=null, alternateCurrency=null without TypeScript error

#### Scenario: Type accepts populated alternate fields

- GIVEN an API response for a cycle with AlternateCurrencyId=USD, ExchangeRate=7.5
- WHEN the response is parsed into CycleListItem
- THEN alternateCurrencyId, exchangeRate, and alternateCurrency fields are accessible and typed correctly

---

### Requirement: REQ-CYC-FORM-1 — CycleForm Alternate Currency Inputs

`CycleForm.vue` MUST include an alternate currency dropdown and an exchange rate numeric input. The alternate currency dropdown MUST use the same currency list source as the default currency dropdown. The exchange rate input MUST only be enabled and visible when an alternate currency is selected. The exchange rate label MUST express direction as "X [defaultCurrencyCode] = 1 [alternateCurrencyCode]" using the currently selected currency codes.

#### Scenario: Exchange rate input hidden when no alternate currency

- GIVEN the CycleForm renders with no alternate currency selected
- WHEN the form is displayed
- THEN the exchange rate input is not visible or is disabled

#### Scenario: Exchange rate input shown when alternate currency selected

- GIVEN the user selects an alternate currency in the dropdown
- WHEN the form updates
- THEN the exchange rate input becomes visible and enabled
- AND the label reads "X [defaultCode] = 1 [alternateCode]"

#### Scenario: Exchange rate label reflects selected currencies

- GIVEN defaultCurrency=GTQ and alternateCurrency=USD selected
- WHEN the exchange rate label renders
- THEN the label reads "X GTQ = 1 USD" (or equivalent localized pattern)

---

### Requirement: REQ-CYC-FORM-2 — CycleForm Pair Validation

`CycleForm.vue` MUST enforce client-side pair validation: `alternateCurrencyId` and `exchangeRate` MUST both be filled or both empty. Submitting with only one field filled MUST be prevented with an inline validation message.

#### Scenario: Only alternate currency filled — submission blocked

- GIVEN the user selects an alternate currency but leaves exchange rate empty
- WHEN they attempt to submit the form
- THEN submission is blocked and an inline error is shown for the exchange rate field

#### Scenario: Only exchange rate filled — submission blocked

- GIVEN the user enters an exchange rate but leaves alternate currency unselected
- WHEN they attempt to submit the form
- THEN submission is blocked and an inline error is shown for the alternate currency field

#### Scenario: Both fields filled — submission allowed

- GIVEN the user has selected an alternate currency and entered a positive exchange rate
- WHEN they submit the form
- THEN no pair validation error is raised and the form submits

#### Scenario: Both fields empty — submission allowed

- GIVEN the user has left both alternate currency and exchange rate empty
- WHEN they submit the form
- THEN no pair validation error is raised

---

### Requirement: REQ-CYC-DETAIL-1 — CycleDetailView Alternate Currency Display

`CycleDetailView.vue` MUST display the cycle's alternate currency info and exchange rate when present. The exchange rate display MUST reflect direction semantics in the format "X [defaultCurrencyCode] = 1 [alternateCurrencyCode]".

#### Scenario: Alternate currency section shown when present

- GIVEN a cycle with alternateCurrency.code="USD" and exchangeRate=7.5 and defaultCurrency.code="GTQ"
- WHEN the cycle detail view renders
- THEN the alternate currency section is visible and shows "7.5 GTQ = 1 USD"

#### Scenario: Alternate currency section absent when not set

- GIVEN a cycle with alternateCurrency=null
- WHEN the cycle detail view renders
- THEN no alternate currency section or exchange rate is displayed

---

### Requirement: REQ-CYC-2 — Cycle Create (admin only)

An admin user MUST see a "New Cycle" button registered as a context action via `layoutStore.pageActions`. Clicking it MUST open a form/modal to submit `POST /api/budgets/{budgetId}/cycles`. Non-admin users MUST NOT see the button.

#### Scenario: Admin creates a cycle

- GIVEN the user has `admin` role and the Cycles tab is active
- WHEN they click "New Cycle" and submit valid name, startDate, endDate
- THEN `POST /cycles` is called and the new cycle appears in the list

#### Scenario: Non-admin sees no create button

- GIVEN the user has `operator` role
- WHEN the Cycles tab renders
- THEN the "New Cycle" button is not present in the navbar action area

---

### Requirement: REQ-CYC-3 — Cycle Edit (admin only)

An admin user MUST be able to edit cycle name and dates. The edit action MUST call `PUT /api/budgets/{budgetId}/cycles/{cycleId}`. Non-admin users MUST NOT see edit controls.

#### Scenario: Admin edits cycle name

- GIVEN an admin user and an existing cycle
- WHEN they open the edit form and change the name
- THEN `PUT /cycles/{cycleId}` is called with the updated name

---

### Requirement: REQ-CYC-4 — Cycle Delete (admin only)

An admin user MUST be able to delete a cycle. The action MUST call `DELETE /api/budgets/{budgetId}/cycles/{cycleId}`. A confirmation prompt MUST be shown before deletion.

#### Scenario: Admin deletes a cycle with confirmation

- GIVEN an admin user and a cycle in the list
- WHEN they click delete and confirm the prompt
- THEN `DELETE /cycles/{cycleId}` is called and the cycle is removed from the list

#### Scenario: Cancelling confirmation retains the cycle

- GIVEN an admin user clicks delete
- WHEN they dismiss the confirmation prompt
- THEN the cycle remains in the list and no API call is made

---

### Requirement: REQ-CYC-5 — Set Active Cycle (admin only)

An admin user MUST be able to mark a cycle as active via `PUT /api/budgets/{budgetId}/active-cycle`. Only one cycle MAY be active at a time; the UI MUST reflect the active state with a badge.

#### Scenario: Admin activates a cycle

- GIVEN the admin clicks "Set Active" on an inactive cycle
- WHEN the action completes
- THEN `PUT /active-cycle` is called and the active badge moves to the selected cycle

---

### Requirement: REQ-PER-1 — Period List within Cycle Detail

Clicking a cycle MUST navigate to a cycle detail view with a breadcrumb (Budget > Cycles > [Cycle name]). The detail view MUST list all periods from `CycleDetailResponse.periods`.

#### Scenario: Cycle detail shows breadcrumb and periods

- GIVEN a cycle with three periods
- WHEN the user clicks on that cycle
- THEN the cycle detail view shows the breadcrumb and lists three periods

---

### Requirement: REQ-PER-2 — Period Create (admin only)

An admin user MUST be able to create a period via `POST /api/budgets/{budgetId}/cycles/{cycleId}/periods`. The form MUST collect name, startDate, endDate.

#### Scenario: Admin creates a period

- GIVEN an admin user is on the cycle detail view
- WHEN they submit the new period form with valid data
- THEN `POST /cycles/{cycleId}/periods` is called and the period appears in the list

---

### Requirement: REQ-PER-3 — Period Edit (admin only)

An admin user MUST be able to edit a period's name and dates via `PUT /api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}`.

#### Scenario: Admin edits a period

- GIVEN an admin user on cycle detail
- WHEN they edit the period name and submit
- THEN `PUT /cycles/{cycleId}/periods/{periodId}` is called with updated data

---

### Requirement: REQ-PER-4 — Period Status Change (admin only)

An admin user MUST be able to change a period's status via `PATCH /api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status`.

#### Scenario: Admin changes period status

- GIVEN an admin user and a period with status "Draft"
- WHEN they change the status to "Open"
- THEN `PATCH /cycles/{cycleId}/periods/{periodId}/status` is called

---

### Requirement: REQ-PER-5 — Period Delete (admin only)

An admin user MUST be able to delete a period via `DELETE /api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}`. A confirmation prompt MUST be shown before deletion.

#### Scenario: Admin deletes a period

- GIVEN an admin user confirms period deletion
- WHEN the action completes
- THEN `DELETE /cycles/{cycleId}/periods/{periodId}` is called and the period is removed

---

### Requirement: REQ-CAT-1 — CategoryGroup and Category Tree

The Categories tab MUST display all category groups with their nested categories via `GET /api/budgets/{budgetId}/category-groups`. Groups MUST be sorted by `displayOrder`. An empty state with a guided prompt MUST be shown when no groups exist.

#### Scenario: Tree renders with groups and categories

- GIVEN the budget has two groups each with two categories
- WHEN the Categories tab is active
- THEN two groups are shown, each expanded to reveal their two categories

---

### Requirement: REQ-CAT-2 — CategoryGroup CRUD (admin only)

An admin user MUST be able to create (`POST /category-groups`), edit (`PUT /category-groups/{groupId}`), and delete (`DELETE /category-groups/{groupId}`) category groups. Non-admin users MUST NOT see write controls.

#### Scenario: Admin creates a category group

- GIVEN an admin user on the Categories tab
- WHEN they submit the new group form
- THEN `POST /category-groups` is called and the group appears in the tree

#### Scenario: Admin deletes a category group with confirmation

- GIVEN an admin user confirms group deletion
- THEN `DELETE /category-groups/{groupId}` is called and the group is removed

---

### Requirement: REQ-CAT-3 — CategoryGroup Drag-and-Drop Reorder (admin only)

An admin user MUST be able to reorder category groups by dragging. On drop, the system MUST call `PUT /api/budgets/{budgetId}/category-groups/order` with the new order. Non-admin users MUST NOT be able to drag.

#### Scenario: Admin reorders category groups

- GIVEN two category groups displayed in order [A, B]
- WHEN an admin drags group B above group A and drops
- THEN `PUT /category-groups/order` is called with the new sequence
- AND the UI reflects the new order [B, A]

---

### Requirement: REQ-CAT-4 — Category CRUD (admin only)

An admin user MUST be able to create (`POST /category-groups/{groupId}/categories`), edit (`PUT /.../{categoryId}`), and delete (`DELETE /.../{categoryId}`) categories within a group.

#### Scenario: Admin creates a category

- GIVEN an admin user on the Categories tab
- WHEN they submit the new category form under a group
- THEN `POST /category-groups/{groupId}/categories` is called

---

### Requirement: REQ-CAT-5 — Category Drag-and-Drop Reorder (admin only)

An admin user MUST be able to reorder categories within a group. On drop, the system MUST call `PUT /api/budgets/{budgetId}/category-groups/{groupId}/categories/order`.

#### Scenario: Admin reorders categories within a group

- GIVEN categories [X, Y] in a group
- WHEN an admin drags Y above X and drops
- THEN `PUT /category-groups/{groupId}/categories/order` is called with the new sequence

---

### Requirement: REQ-BL-1 — BudgetLine List

Selecting a period (accessible from cycle detail) MUST navigate to a budget line view that loads all lines via `GET /api/budgets/{budgetId}/periods/{periodId}/lines`. The view MUST display name, lineType, isRecurring, categoryGroup, category, budgetedAmount, and currency.

#### Scenario: Budget lines listed for a period

- GIVEN a period with three budget lines
- WHEN the user navigates to the budget line view for that period
- THEN three rows are displayed with the correct fields

---

### Requirement: REQ-BL-2 — Inline BudgetLine Creation (operator+)

The budget line view MUST provide an inline empty row at the bottom of the table. Users with at least `operator` role MUST be able to fill in the row and submit to create a line via `POST /api/budgets/{budgetId}/periods/{periodId}/lines`. The inline row MUST include a category dropdown filtered to categories belonging to the same group selected in the group dropdown. Read-only users MUST NOT see the inline row.

#### Scenario: Operator creates a budget line inline

- GIVEN an operator user on the budget line view
- WHEN they fill in the inline row and press enter/submit
- THEN `POST /periods/{periodId}/lines` is called and the new line appears in the table

#### Scenario: Read-only user sees no inline row

- GIVEN a user with `read-only` role
- WHEN the budget line view renders
- THEN no inline creation row is visible

#### Scenario: Category dropdown filters by selected group

- GIVEN the user has selected Group A in the inline row's group dropdown
- WHEN the category dropdown opens
- THEN only categories belonging to Group A are listed

#### Scenario: Category dropdown resets when group changes

- GIVEN the user has selected Group A and Category X in the inline row
- WHEN the user changes the group dropdown to Group B
- THEN the category selection clears and the category dropdown shows only Group B's categories

#### Scenario: Category dropdown is empty when group has no categories

- GIVEN Group C has no categories
- WHEN the user selects Group C in the inline row
- THEN the category dropdown shows an empty state

---

### Requirement: REQ-BL-3 — BudgetLine Edit via Modal (operator+)

Double-clicking a budget line row MUST open a full edit modal. The modal MUST NOT trigger text selection (dblclick handler MUST call `window.getSelection()?.removeAllRanges()` before opening). Submitting the modal MUST call `PUT /api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}`. Read-only users MUST NOT be able to open the edit modal.

#### Scenario: Operator double-clicks to edit

- GIVEN an operator user and a budget line row
- WHEN they double-click the row
- THEN an edit modal opens pre-populated with the line's current values
- AND no browser text selection is visible

#### Scenario: Operator submits edit modal

- GIVEN the edit modal is open with changes
- WHEN the operator submits
- THEN `PUT /periods/{periodId}/lines/{lineId}` is called and the row reflects the update

#### Scenario: No text selected after dblclick on group row

- GIVEN any matrix group, category, or line row
- WHEN the user double-clicks the row
- THEN `window.getSelection()?.removeAllRanges()` is called and no text is highlighted

---

### Requirement: REQ-BL-4 — BudgetLine Delete (operator+)

Users with at least `operator` role MUST be able to delete a budget line via `DELETE /api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}`. A confirmation prompt MUST be shown.

#### Scenario: Operator deletes a budget line

- GIVEN an operator user confirms budget line deletion
- THEN `DELETE /periods/{periodId}/lines/{lineId}` is called and the row is removed

---

### Requirement: REQ-MATRIX-DND-1 — Matrix Row Drag-and-Drop Reorder

The budget matrix view MUST support drag-and-drop reorder for Groups, Categories, and Lines using `vue-draggable-plus`. Reorder via drag MUST be available regardless of whether the period is open or closed. On drop, the system MUST call the same reorder endpoint used by arrow buttons. Arrow-button and drag-and-drop reorder MUST NOT conflict; only one mechanism is active at a time in the UI.

#### Scenario: Operator drags a group to a new position

- GIVEN an operator user and a matrix with groups [G1, G2, G3]
- WHEN they drag G3 above G1 and drop
- THEN the group reorder endpoint is called with the new order
- AND the matrix reflects [G3, G1, G2]

#### Scenario: Operator drags a line on a closed period

- GIVEN Period.IsClosed = true and a matrix with multiple lines
- WHEN the operator drags a line to a new position
- THEN the line reorder endpoint is called and the order updates
- AND no PERIOD_CLOSED error is returned (reorder is structural, not period-scoped)

#### Scenario: Drag reorder and arrow buttons both persist to same endpoint

- GIVEN two lines in a category
- WHEN reorder is performed via drag OR via arrow button
- THEN both actions call the same backend reorder endpoint

---

### Requirement: REQ-MATRIX-FOOTER-1 — Matrix Summary Footer Order and Labels

The budget matrix summary footer MUST display subtotals in the following fixed order: Expenses, PreventiveSavings, LongTermSavings. Each subtotal row MUST be labeled "SubTotal". A Total row MUST appear below the three SubTotal rows and MUST display the arithmetic sum of all three SubTotal values.

#### Scenario: Footer renders in correct order

- GIVEN a matrix with execution data across all three budget types
- WHEN the summary footer renders
- THEN rows appear in order: Expenses SubTotal → PreventiveSavings SubTotal → LongTermSavings SubTotal → Total

#### Scenario: Total row equals sum of three subtotals

- GIVEN Expenses SubTotal = 1000, PreventiveSavings SubTotal = 200, LongTermSavings SubTotal = 300
- WHEN the footer renders
- THEN the Total row displays 1500

#### Scenario: Footer labels use "SubTotal" text

- GIVEN the matrix summary footer is rendered
- WHEN the user views any of the three category rows
- THEN each row label reads "SubTotal" (not the former label)

---

### Requirement: REQ-MATRIX-RENDER-1 — Incremental Refresh on Name-Only Edits

When a group or category name is edited and saved, the matrix view MUST update the display name incrementally without triggering a full matrix data reload.

#### Scenario: Group name edit does not reload matrix data

- GIVEN an operator edits only the name of a CategoryGroup
- WHEN the edit is saved
- THEN the group row name updates in-place
- AND no full matrix fetch (GET /periods/{periodId}/matrix or equivalent) is triggered

#### Scenario: Category name edit does not reload matrix data

- GIVEN an operator edits only the name of a Category
- WHEN the edit is saved
- THEN the category row name updates in-place
- AND no full matrix fetch is triggered

---

### Requirement: REQ-TOGGLE-1 — Show-Deleted Toggle (Structure Entities)

CycleListView, CycleDetailView (Periods), CategoryTreeView (CategoryGroups + Categories), and BudgetLinesView MUST each display a "Show deleted" toggle. The toggle state MUST be stored in Pinia session state (not persisted to localStorage/URL). The default value MUST be `false` on first load. When toggled `true`, the view MUST reload its list with `includeDeleted=true` (or the equivalent API flag). Soft-deleted items MUST be visually distinguished (e.g., muted or strikethrough).

#### Scenario: Default state is OFF

- GIVEN the user navigates to CycleListView for the first time in a session
- WHEN the view renders
- THEN the show-deleted toggle is OFF and only active cycles are shown

#### Scenario: Toggle ON reloads with deleted items

- GIVEN CycleListView with 1 active and 1 deleted Cycle
- WHEN the user switches the show-deleted toggle to ON
- THEN `GET /cycles?includeDeleted=true` is called and both cycles appear in the list
- AND the deleted cycle is visually distinguished from the active one

#### Scenario: Toggle OFF reloads without deleted items

- GIVEN the toggle is ON and deleted items are visible
- WHEN the user switches the toggle to OFF
- THEN `GET /cycles` (no includeDeleted param) is called and only active cycles appear

#### Scenario: Toggle state is session-scoped

- GIVEN the user sets the toggle ON in CycleListView
- WHEN the user navigates away and returns in the same session
- THEN the toggle remains ON

#### Scenario: Toggle state resets on new session

- GIVEN a new browser session (Pinia state reset)
- WHEN the user opens any structure view
- THEN the toggle defaults to OFF

---

### Requirement: REQ-RESTORE-1 — Restore Action on Soft-Deleted Items

When the show-deleted toggle is ON, each soft-deleted item in CycleListView, CycleDetailView, CategoryTreeView, and BudgetLinesView MUST display a Restore button. Clicking Restore MUST call the appropriate restore endpoint. On success, a success toast MUST be pushed via `useToastStore`. The list MUST refresh after a successful restore.

#### Scenario: Restore Cycle

- GIVEN show-deleted toggle is ON and a soft-deleted Cycle is listed
- WHEN the admin clicks "Restore" on the Cycle row
- THEN `POST /cycles/{cycleId}/restore` is called
- AND a success toast is shown
- AND the Cycle no longer appears as soft-deleted in the list

#### Scenario: Restore CategoryGroup

- GIVEN show-deleted toggle is ON and a soft-deleted CategoryGroup is listed
- WHEN the admin clicks "Restore"
- THEN `POST /category-groups/{groupId}/restore` is called and a success toast appears

#### Scenario: Restore Category

- GIVEN show-deleted toggle is ON and a soft-deleted Category is listed
- WHEN the admin clicks "Restore"
- THEN `POST /categories/{categoryId}/restore` is called and a success toast appears

#### Scenario: Restore BudgetLine

- GIVEN show-deleted toggle is ON and a soft-deleted BudgetLine is listed
- WHEN the operator clicks "Restore"
- THEN `POST /periods/{periodId}/budget-lines/{lineId}/restore` is called and a success toast appears

---

### Requirement: REQ-RESTORE-PERIOD-1 — Period Restore with Cascade Disclosure

Before restoring a Period, the UI MUST display a disclosure warning stating that all child BudgetLines will also be restored. The user MUST confirm before the restore endpoint is called.

#### Scenario: Disclosure shown before Period restore

- GIVEN show-deleted toggle is ON and a soft-deleted Period is listed
- WHEN the admin clicks "Restore" on the Period
- THEN a disclosure message is shown: "Restoring this Period will also restore all its BudgetLines."

#### Scenario: User confirms — restore proceeds

- GIVEN the Period restore disclosure is visible
- WHEN the user confirms
- THEN `POST /periods/{periodId}/restore` is called and a success toast appears

#### Scenario: User cancels — no restore

- GIVEN the Period restore disclosure is visible
- WHEN the user cancels
- THEN no API call is made and the Period remains soft-deleted

---

### Requirement: REQ-TOAST-ACTION-1 — Success Toasts on Structure Entity Actions

On successful create, delete, or restore of any structure entity (Cycle, Period, CategoryGroup, Category, BudgetLine), the UI MUST push a success toast via `useToastStore` using the appropriate i18n key. No toast MUST be shown on failed operations (error handling is separate).

#### Scenario: Delete success toast

- GIVEN an admin user deletes a Cycle
- WHEN `DELETE /cycles/{cycleId}` returns 204
- THEN a success toast is shown with the `budgetStructure.cycles.deleteSuccess` message

#### Scenario: Create success toast

- GIVEN an admin user creates a Period
- WHEN `POST /periods` returns 201
- THEN a success toast is shown with the `budgetStructure.periods.createSuccess` message

#### Scenario: Restore success toast

- GIVEN an admin user restores a CategoryGroup
- WHEN `POST /category-groups/{groupId}/restore` returns 200
- THEN a success toast is shown with the `budgetStructure.categoryGroups.restoreSuccess` message

#### Scenario: No toast on API error

- GIVEN a delete request that returns 500
- WHEN the error is handled
- THEN no success toast is pushed (error state handled separately)

---

### Requirement: REQ-I18N-1 — Budget Structure i18n Keys

All user-visible strings in the budget structure UI MUST use keys under the `budgetStructure.*` namespace in `en.json` and `es.json`. No hardcoded English or Spanish strings MAY appear in template markup.

#### Scenario: All budget structure strings are i18n-keyed

- GIVEN the EN and ES locale files
- WHEN all `budgetStructure.*` keys are present
- THEN rendering the budget structure UI in either locale shows fully translated text with no fallback warnings

---

### Requirement: REQ-FIX-1 — Scalar API Reference Endpoint

The backend MUST expose the Scalar API explorer at `/scalar/v1` by wiring `app.MapScalarApiReference()` after `app.MapOpenApi()` in `Program.cs`.

#### Scenario: Scalar UI accessible after fix

- GIVEN the application is running with the Scalar line added
- WHEN a browser requests `/scalar/v1`
- THEN the Scalar API reference UI is rendered

---

### Requirement: REQ-FIX-2 — vue-i18n Linked-Message @ Bug

The four email placeholder values containing `@` in `en.json` and `es.json` MUST be escaped as `{'@'}` to prevent vue-i18n runtime linked-message errors.

Affected locations:
| File | Line | Key |
|---|---|---|
| `en.json` | 13 | `auth.login.emailPlaceholder` |
| `en.json` | 23 | `auth.register.emailPlaceholder` |
| `es.json` | 13 | `auth.login.emailPlaceholder` |
| `es.json` | 23 | `auth.register.emailPlaceholder` |

#### Scenario: No linked-message error at login

- GIVEN the `@` characters are escaped in all four locations
- WHEN the login page renders
- THEN no vue-i18n runtime warning or error appears in the browser console

---

### Requirement: REQ-FIX-3 — Login/Register Visual Alignment

`LoginView` and `RegisterView` MUST render correctly under `PublicLayout` with daisyUI v5 form-control semantics. The "Language" label in `RegisterView` MUST use an i18n key (`auth.register.languageLabel`) instead of hardcoded text.

#### Scenario: Language label uses i18n key

- GIVEN the fix is applied and locale is set to "es"
- WHEN the register page renders
- THEN the language selector label shows the Spanish translation, not the hardcoded "Language" string
