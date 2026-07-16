# Delta for budget-structure-ui

## MODIFIED Requirements

### Requirement: REQ-BL-2 — Inline BudgetLine Creation (operator+)

The budget line view MUST provide an inline empty row at the bottom of the table. Users with at least `operator` role MUST be able to fill in the row and submit to create a line via `POST /api/budgets/{budgetId}/periods/{periodId}/lines`. The inline row MUST include a category dropdown filtered to categories belonging to the same group selected in the group dropdown. Read-only users MUST NOT see the inline row.
(Previously: inline row existed but category dropdown was not filtered by the selected group)

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
(Previously: double-click did not guard against text selection in MatrixGroupRow, MatrixCategoryRow, MatrixLineRow)

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

## ADDED Requirements

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
