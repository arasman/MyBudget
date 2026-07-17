# Delta for budget-execution

## ADDED Requirements

### Requirement: REQ-EXEC-CONFIRM-1 — ExecutionRecord Two-Step Delete Confirmation

The UI MUST require a two-step confirmation before soft-deleting an ExecutionRecord. On the first
click of the Delete button, the button MUST change to a confirmation state (e.g., "Confirm?" with
a cancel option). On the second click, the delete call MUST proceed. This MUST follow the
MatrixLineRow two-step pattern. A cancel action (clicking away or pressing Escape) MUST reset the
button to its initial state without making any API call.

#### Scenario: First click — enters confirmation state

- GIVEN an active ExecutionRecord row and an open Period
- WHEN the user clicks the Delete button once
- THEN the button renders in its "confirm" state (changed label or highlight)
- AND no API call is made

#### Scenario: Second click — delete proceeds

- GIVEN the Delete button is in confirmation state
- WHEN the user clicks it again
- THEN `DELETE .../executions/{id}` is called
- AND a success toast is shown with the `budgetExecution.record.deleteSuccess` message
- AND the row is removed or marked as deleted

#### Scenario: Cancel resets confirmation state

- GIVEN the Delete button is in confirmation state
- WHEN the user clicks outside the button or presses Escape
- THEN the button reverts to its initial Delete state
- AND no API call is made

#### Scenario: Confirmation state is row-local

- GIVEN two ExecutionRecord rows are visible
- WHEN the user enters the confirmation state on Row A
- THEN Row B's delete button remains in its normal initial state

---

### Requirement: REQ-EXEC-TOAST-1 — Success Toasts on ExecutionRecord Delete and Restore

On successful delete or restore of an ExecutionRecord, the UI MUST push a success toast via
`useToastStore` using the appropriate i18n key. No toast MUST be shown on failed operations.

#### Scenario: Delete success toast

- GIVEN the two-step delete confirmation is confirmed
- WHEN `DELETE .../executions/{id}` returns 204
- THEN a success toast is shown with the `budgetExecution.record.deleteSuccess` message

#### Scenario: Restore success toast

- GIVEN a soft-deleted ExecutionRecord in a view with includeDeleted=true
- WHEN `POST .../executions/{id}/restore` returns 200
- THEN a success toast is shown with the `budgetExecution.record.restoreSuccess` message
