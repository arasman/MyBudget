# budget-structure-ui Specification

## Purpose

Defines the full CRUD UI for budget structure entities: Cycles, Periods, CategoryGroups, Categories, and BudgetLines. Covers navigation model, role gating, empty states, drag-and-drop reorder, and i18n.

---

## Requirements

### Requirement: REQ-NAV-1 — Budget Structure Navigation Tabs

The budget detail view MUST render two tabs: "Cycles" and "Categories". The active tab MUST be reflected in the URL. Navigating between tabs MUST NOT lose the active budget context.

#### Scenario: Default tab on budget entry

- GIVEN the user navigates to `/budgets/:budgetId`
- WHEN the page renders
- THEN the "Cycles" tab is active by default

#### Scenario: Tab switch updates URL

- GIVEN the user is on the Cycles tab
- WHEN they click the "Categories" tab
- THEN the URL reflects the Categories tab route

---

### Requirement: REQ-CYC-1 — Cycle List

The system MUST display all cycles for the active budget via `GET /api/budgets/{budgetId}/cycles`. Each row MUST show name, start date, end date, period count, and active status. An empty state with a guided prompt MUST be shown when no cycles exist.

#### Scenario: Cycles listed

- GIVEN the budget has two cycles
- WHEN the Cycles tab is active
- THEN both cycles are displayed with name, date range, period count, and active badge

#### Scenario: Empty state shown

- GIVEN the budget has no cycles
- WHEN the Cycles tab is active
- THEN a guided empty-state prompt is shown instead of an empty table

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

The budget line view MUST provide an inline empty row at the bottom of the table. Users with at least `operator` role MUST be able to fill in the row and submit to create a line via `POST /api/budgets/{budgetId}/periods/{periodId}/lines`. Read-only users MUST NOT see the inline row.

#### Scenario: Operator creates a budget line inline

- GIVEN an operator user on the budget line view
- WHEN they fill in the inline row and press enter/submit
- THEN `POST /periods/{periodId}/lines` is called and the new line appears in the table

#### Scenario: Read-only user sees no inline row

- GIVEN a user with `read-only` role
- WHEN the budget line view renders
- THEN no inline creation row is visible

---

### Requirement: REQ-BL-3 — BudgetLine Edit via Modal (operator+)

Double-clicking a budget line row MUST open a full edit modal. Submitting the modal MUST call `PUT /api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}`. Read-only users MUST NOT be able to open the edit modal.

#### Scenario: Operator double-clicks to edit

- GIVEN an operator user and a budget line row
- WHEN they double-click the row
- THEN an edit modal opens pre-populated with the line's current values

#### Scenario: Operator submits edit modal

- GIVEN the edit modal is open with changes
- WHEN the operator submits
- THEN `PUT /periods/{periodId}/lines/{lineId}` is called and the row reflects the update

---

### Requirement: REQ-BL-4 — BudgetLine Delete (operator+)

Users with at least `operator` role MUST be able to delete a budget line via `DELETE /api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}`. A confirmation prompt MUST be shown.

#### Scenario: Operator deletes a budget line

- GIVEN an operator user confirms budget line deletion
- THEN `DELETE /periods/{periodId}/lines/{lineId}` is called and the row is removed

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
