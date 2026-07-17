# Delta for budget-structure-ui

## ADDED Requirements

### Requirement: REQ-TOGGLE-1 — Show-Deleted Toggle (Structure Entities)

CycleListView, CycleDetailView (Periods), CategoryTreeView (CategoryGroups + Categories), and
BudgetLinesView MUST each display a "Show deleted" toggle. The toggle state MUST be stored in
Pinia session state (not persisted to localStorage/URL). The default value MUST be `false` on
first load. When toggled `true`, the view MUST reload its list with `includeDeleted=true` (or the
equivalent API flag). Soft-deleted items MUST be visually distinguished (e.g., muted or strikethrough).

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

When the show-deleted toggle is ON, each soft-deleted item in CycleListView, CycleDetailView,
CategoryTreeView, and BudgetLinesView MUST display a Restore button. Clicking Restore MUST call
the appropriate restore endpoint. On success, a success toast MUST be pushed via `useToastStore`.
The list MUST refresh after a successful restore.

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

Before restoring a Period, the UI MUST display a disclosure warning stating that all child
BudgetLines will also be restored. The user MUST confirm before the restore endpoint is called.

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

On successful create, delete, or restore of any structure entity (Cycle, Period, CategoryGroup,
Category, BudgetLine), the UI MUST push a success toast via `useToastStore` using the appropriate
i18n key. No toast MUST be shown on failed operations (error handling is separate).

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
