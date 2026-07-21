# Spec: budget-line-redesign

Artifact store: hybrid
Capabilities modified: budget-structure, budget-execution, budget-structure-ui

---

## Delta: budget-structure

### MODIFIED: REQ-BL-NAME-1 — BudgetLine Name Uniqueness per Budget

The system MUST reject creating or updating a BudgetLine when the same name already exists within the same Budget (scoped by `BudgetId` only), including soft-deleted lines. The uniqueness check MUST be enforced via a DB-level `UNIQUE(BudgetId, Name)` index with no filter clause (soft-deleted rows are included).

(Previously: uniqueness was scoped to `(CategoryGroupId, CategoryId)` pair within a Period)

#### Scenario: Create duplicate name rejected across budget `@integration`
- GIVEN a BudgetLine named "Rent" (active or soft-deleted) in Budget B1
- WHEN POST `/api/budgets/{budgetId}/lines` with Name="Rent"
- THEN HTTP 422 with error code `BUDGET_LINE_NAME_DUPLICATE`

#### Scenario: Self-rename allowed `@integration`
- GIVEN a BudgetLine "Rent" being updated (same lineId)
- WHEN PUT with Name="Rent"
- THEN HTTP 200 (self-exclusion applies)

#### Scenario: Same name in different budget allowed `@integration`
- GIVEN a BudgetLine named "Rent" in Budget B1
- WHEN POST `/api/budgets/{B2}/lines` with Name="Rent"
- THEN HTTP 201 (different budget scope)

---

### MODIFIED: REQ-BL-01 — IsClosed Guard

The system MUST reject CreateBudgetLine, UpdateBudgetLine, and DeleteBudgetLine ONLY when the update targets a revision split with `ValidFrom` in a closed period. The `IsClosed` guard no longer blocks metadata-only edits (name, categoryGroupId, categoryId). The route no longer includes `periodId`.

(Previously: all BudgetLine mutations on a closed Period returned PERIOD_CLOSED regardless of field)

#### Scenario: Revision split with ValidFrom in closed period blocked `@integration`
- GIVEN a Period with `IsClosed=true` covering a date range
- WHEN PUT `.../lines/{lineId}` with `ValidFrom` falling within that closed period
- THEN HTTP 409 with error code `PERIOD_CLOSED`

#### Scenario: Metadata-only update on budget with closed periods allowed `@integration`
- GIVEN all Periods in a Budget are closed
- WHEN PUT `.../lines/{lineId}` updating only Name (no amount revision fields)
- THEN HTTP 200

---

### MODIFIED: REQ-BL-02 — Create BudgetLine

The system MUST allow creating a BudgetLine under a Budget (not a Period). The command MUST provide `StartDate` (required), `EndDate` (optional, null = perpetual), `InitialAmount` (required, > 0), and `CurrencyId`. The handler MUST create the BudgetLine and an initial BudgetLineRevision covering `[StartDate, EndDate]` in a single transaction. `PeriodId` and `IsRecurring` MUST NOT be accepted.

(Previously: creation required `PeriodId` and `IsRecurring`; route was `/periods/{periodId}/lines`)

#### Scenario: Happy path with finite date range `@integration`
- GIVEN a Budget with valid CategoryGroupId, StartDate=2025-01-01, EndDate=2025-12-31, InitialAmount=1500, CurrencyId=GTQ
- WHEN POST `/api/budgets/{budgetId}/lines`
- THEN HTTP 201; BudgetLine row created; one BudgetLineRevision with ValidFrom=2025-01-01, ValidTo=2025-12-31, Amount=1500

#### Scenario: Happy path with perpetual end date `@integration`
- GIVEN StartDate=2025-01-01, EndDate=null
- WHEN POST `/api/budgets/{budgetId}/lines`
- THEN HTTP 201; BudgetLineRevision.ValidTo = null

#### Scenario: EndDate before StartDate rejected `@unit`
- GIVEN StartDate=2025-06-01, EndDate=2025-05-31
- WHEN validator runs
- THEN HTTP 422 with validation error on EndDate

#### Scenario: InitialAmount zero rejected `@unit`
- GIVEN InitialAmount = 0
- WHEN validator runs
- THEN HTTP 422 with validation error on InitialAmount

---

### MODIFIED: REQ-BL-03 — Update BudgetLine

The system MUST allow updating a BudgetLine. Metadata changes (Name, CategoryGroupId, CategoryId) MUST be saved without affecting revisions. Amount revision changes MUST be triggered when `ValidFrom`, `NewAmount`, and `CurrencyId` are provided; the handler MUST call `BudgetLine.SplitRevision(ValidFrom, ValidTo, NewAmount, CurrencyId)` to produce a gapless revision split. `IsRecurring` MUST NOT be accepted.

(Previously: update unconditionally created a new BudgetLineRevision with `RevisedAt`; `IsRecurring` was updatable)

#### Scenario: Metadata-only update `@integration`
- GIVEN a BudgetLine with existing revisions
- WHEN PUT `.../lines/{lineId}` with only Name changed
- THEN HTTP 200; Name updated; revision count unchanged

#### Scenario: Amount revision split `@integration`
- GIVEN a BudgetLine with revision [2025-01-01, null, 1500 GTQ] and ValidFrom=2025-06-01, NewAmount=2000, CurrencyId=GTQ
- WHEN PUT `.../lines/{lineId}`
- THEN HTTP 200; original revision trimmed to ValidTo=2025-05-31; new revision [2025-06-01, null, 2000, GTQ] inserted

#### Scenario: ValidFrom before today rejected `@unit`
- GIVEN ValidFrom = yesterday
- WHEN validator runs
- THEN HTTP 422 with validation error (no retroactive splits)

#### Scenario: ValidFrom outside BudgetLine date range rejected `@unit`
- GIVEN BudgetLine.StartDate=2025-01-01, BudgetLine.EndDate=2025-12-31 and ValidFrom=2026-01-01
- WHEN validator runs
- THEN HTTP 422 with validation error on ValidFrom

---

### MODIFIED: REQ-BL-04 — Delete BudgetLine

The system MUST soft-delete the BudgetLine. The route MUST NOT include `periodId`. The `IsClosed` guard is removed from delete; soft-delete is always permitted.

(Previously: delete route included `periodId`; IsClosed blocked delete on closed periods)

#### Scenario: Soft delete `@integration`
- GIVEN an active BudgetLine
- WHEN DELETE `/api/budgets/{budgetId}/lines/{lineId}`
- THEN HTTP 204; BudgetLine.DeletedAt set

---

### MODIFIED: REQ-BL-05 — Currency and DisplayOrder

**DisplayOrder**: Reorder scope changes from `(PeriodId, CategoryGroupId, CategoryId)` to `(BudgetId, CategoryGroupId, CategoryId)`. Route MUST be `/api/budgets/{budgetId}/lines/order`.

(Previously: reorder was period-scoped; route included `periodId`)

#### Scenario: Reorder BudgetLines at budget scope `@integration`
- GIVEN 3 BudgetLines in a Budget under same CategoryGroup
- WHEN PUT `/api/budgets/{budgetId}/lines/order` with IDs in new order
- THEN HTTP 200; DisplayOrder values reassigned

---

### MODIFIED: REQ-READ-04 — List BudgetLines

The system MUST return all non-deleted BudgetLines for a Budget (scoped by `budgetId` only). Response items MUST include `startDate`, `endDate`, and `budgetedAmount` (from the effective revision for display). Response items MUST NOT include `isRecurring` or `revisedAt`. The route MUST be `GET /api/budgets/{budgetId}/lines`.

(Previously: scoped by `periodId`; included `isRecurring`, `revisedAt`; route was `/periods/{periodId}/lines`)

#### Scenario: Happy path — returns budget-scoped lines `@integration`
- GIVEN a Budget with 3 active BudgetLines
- WHEN GET `/api/budgets/{budgetId}/lines`
- THEN HTTP 200; all 3 lines returned with startDate, endDate, budgetedAmount fields present; no isRecurring field

#### Scenario: Response excludes isRecurring `@unit`
- GIVEN any BudgetLine
- WHEN GET `.../lines`
- THEN response items do not contain `isRecurring` or `revisedAt` properties

---

### ADDED: REQ-BL-ENTITY-1 — BudgetLine Date Range Fields

`BudgetLine` MUST have `StartDate` (DateOnly, required) and `EndDate` (DateOnly?, nullable). `PeriodId` and `IsRecurring` MUST NOT exist on the entity. The factory `Create()` MUST accept `startDate`, `endDate` and MUST NOT accept `periodId`, `isRecurring`.

#### Scenario: BudgetLine created with date range `@unit`
- GIVEN BudgetLine.Create(budgetId, ..., startDate=2025-01-01, endDate=null)
- WHEN factory runs
- THEN BudgetLine.StartDate=2025-01-01, BudgetLine.EndDate=null

---

### ADDED: REQ-BL-REVISION-1 — BudgetLineRevision Date Range Fields

`BudgetLineRevision` MUST have `ValidFrom` (DateOnly, required) and `ValidTo` (DateOnly?, nullable). `RevisedAt` MUST NOT exist on the entity. The factory `Create()` MUST accept `validFrom`, `validTo`.

#### Scenario: BudgetLineRevision created with ValidFrom/ValidTo `@unit`
- GIVEN BudgetLineRevision.Create(budgetLineId, validFrom=2025-01-01, validTo=null, amount=1500, currencyId=GTQ)
- WHEN factory runs
- THEN ValidFrom=2025-01-01, ValidTo=null

---

### ADDED: REQ-BL-SPLIT-1 — Gapless Revision Invariant via SplitRevision

`BudgetLine` MUST expose a `SplitRevision(DateOnly newValidFrom, DateOnly? newValidTo, decimal amount, Guid currencyId)` domain method that produces a non-overlapping, gap-free revision timeline:

1. Finds the enclosing revision where `ValidFrom <= newValidFrom AND (ValidTo IS NULL OR ValidTo >= newValidFrom)`.
2. Sets enclosing revision `ValidTo = newValidFrom.AddDays(-1)`.
3. Creates new revision `[newValidFrom, newValidTo, amount, currencyId]`.
4. If `newValidTo` is not null and the enclosing revision's original `ValidTo` is null or > `newValidTo`: creates a tail revision `[newValidTo.AddDays(1), enclosing.OriginalValidTo, enclosing.BudgetedAmount, enclosing.CurrencyId]`.

#### Scenario: Split creates head, new, and tail segments `@unit`
- GIVEN revision [2025-01-01, null, 1500, GTQ]
- WHEN SplitRevision(newValidFrom=2025-06-01, newValidTo=2025-08-31, amount=2000, currencyId=GTQ)
- THEN revisions: [2025-01-01, 2025-05-31, 1500, GTQ], [2025-06-01, 2025-08-31, 2000, GTQ], [2025-09-01, null, 1500, GTQ]

#### Scenario: Split with open-ended new revision — no tail created `@unit`
- GIVEN revision [2025-01-01, null, 1500, GTQ]
- WHEN SplitRevision(newValidFrom=2025-06-01, newValidTo=null, amount=2000, currencyId=GTQ)
- THEN revisions: [2025-01-01, 2025-05-31, 1500, GTQ], [2025-06-01, null, 2000, GTQ]

#### Scenario: No enclosing revision — error `@unit`
- GIVEN revision [2025-01-01, 2025-06-30, 1500, GTQ]
- WHEN SplitRevision(newValidFrom=2025-08-01, ...)
- THEN domain exception (no enclosing revision found)

---

### MODIFIED: REQ-CYC-03 — Delete Cycle (BudgetLine cascade removed)

The system MUST soft-delete the Cycle and cascade-soft-delete all its Periods and their child ExecutionRecords. BudgetLines MUST NOT be cascade-deleted when a Cycle or Period is deleted (BudgetLines are Budget-level, not Period-level).

(Previously: DeleteCycle and DeletePeriod cascaded soft-delete to BudgetLines via PeriodId)

#### Scenario: Soft delete Cycle — BudgetLines unaffected `@integration`
- GIVEN a Cycle with Periods, and Budget-level BudgetLines overlapping those periods
- WHEN DELETE `/api/budgets/{id}/cycles/{cycleId}`
- THEN Cycle and its Periods have `DeletedAt` set; BudgetLines remain active (DeletedAt = null)

---

### MODIFIED: REQ-RST-02 — RestoreCycle (BudgetLine cascade removed)

Restores Cycle and all its soft-deleted Periods. MUST NOT cascade-restore BudgetLines (BudgetLines are Budget-level).

(Previously: RestoreCycle restored BudgetLines of restored Periods via PeriodId)

#### Scenario: Restore Cycle — BudgetLines not affected `@integration`
- GIVEN soft-deleted Cycle with Periods; BudgetLines at Budget level are independently managed
- WHEN POST `.../cycles/{cycleId}/restore`
- THEN Cycle and Periods restored; BudgetLines not modified by this operation

---

### MODIFIED: REQ-RST-05 — RestoreBudgetLine

Route changes to `POST /budgets/{budgetId}/lines/{lineId}/restore`. No longer requires `periodId` in route.

(Previously: route was `/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/restore`)

#### Scenario: Restore BudgetLine without periodId `@integration`
- GIVEN a soft-deleted BudgetLine
- WHEN POST `/api/budgets/{budgetId}/lines/{lineId}/restore`
- THEN HTTP 200; BudgetLine.DeletedAt = null

---

### REMOVED: REQ-PER-04 — Period cascade to BudgetLines

(Reason: BudgetLines are now Budget-level entities; Period delete no longer cascades to BudgetLines)
(Migration: Delete Period → soft-delete Period only; BudgetLines independently managed)

---

## Delta: budget-execution

### MODIFIED: REQ-EXEC-7 — BudgetLine Period Validation

The `PeriodId == BudgetLine.PeriodId` check MUST be replaced. The handler MUST validate that the Period's `StartDate` falls within `BudgetLine.StartDate..BudgetLine.EndDate` (i.e., `Period.StartDate >= BudgetLine.StartDate AND (BudgetLine.EndDate IS NULL OR Period.StartDate <= BudgetLine.EndDate)`). A mismatch MUST be rejected with HTTP 422 and error code `BUDGET_LINE_NOT_IN_PERIOD`.

(Previously: validated `BudgetLine.PeriodId == route.periodId`, rejected with PERIOD_MISMATCH)

#### Scenario: BudgetLine covers the period — accepted `@integration`
- GIVEN BudgetLine.StartDate=2025-01-01, BudgetLine.EndDate=null; Period.StartDate=2025-03-01
- WHEN POST `.../executions` for that line and period
- THEN HTTP 201 Created

#### Scenario: BudgetLine does not cover the period — rejected `@integration`
- GIVEN BudgetLine.StartDate=2025-06-01; Period.StartDate=2025-03-01
- WHEN POST `.../executions`
- THEN HTTP 422 with error code `BUDGET_LINE_NOT_IN_PERIOD`

#### Scenario: Perpetual BudgetLine always covers any period `@integration`
- GIVEN BudgetLine.StartDate=2020-01-01, BudgetLine.EndDate=null; Period.StartDate=2030-01-01
- WHEN POST `.../executions`
- THEN HTTP 201 (perpetual coverage)

---

### MODIFIED: REQ-EXEC-DATE-RANGE-1 — OperationDate Combined Range Check

When `OperationDate` is provided, the backend MUST validate it falls within the combined date range: `OperationDate >= MAX(Period.StartDate, BudgetLine.StartDate)` AND `OperationDate <= MIN(Period.EndDate, BudgetLine.EndDate ?? Period.EndDate)`. A date outside this intersection MUST be rejected with error code `OPERATION_DATE_OUT_OF_RANGE` (422).

(Previously: validated only against Period.StartDate..Period.EndDate; no BudgetLine date range check)

#### Scenario: OperationDate within period and BudgetLine range accepted `@integration`
- GIVEN Period=Jan 2025, BudgetLine.StartDate=2025-01-15, OperationDate=2025-01-20
- WHEN CreateExecution
- THEN HTTP 201 Created

#### Scenario: OperationDate before BudgetLine StartDate rejected `@integration`
- GIVEN Period.StartDate=2025-01-01, BudgetLine.StartDate=2025-01-15, OperationDate=2025-01-10
- WHEN CreateExecution
- THEN HTTP 422, error code `OPERATION_DATE_OUT_OF_RANGE`

#### Scenario: OperationDate after BudgetLine EndDate rejected `@integration`
- GIVEN Period.EndDate=2025-01-31, BudgetLine.EndDate=2025-01-20, OperationDate=2025-01-25
- WHEN CreateExecution
- THEN HTTP 422, error code `OPERATION_DATE_OUT_OF_RANGE`

#### Scenario: OperationDate null — no range check `@unit`
- GIVEN OperationDate = null
- WHEN validator runs
- THEN no date-range error

---

### MODIFIED: REQ-EXEC-TOTALS-1 — ListPeriodExecutionTotals BudgetLine Filter

The SQL query MUST join BudgetLines via date-range intersection instead of `PeriodId` equality. A BudgetLine is active for a Period when `BudgetLine.StartDate <= Period.StartDate AND (BudgetLine.EndDate IS NULL OR BudgetLine.EndDate >= Period.StartDate)`. The effective `BudgetedAmount` for that period MUST come from the revision where `ValidFrom <= Period.StartDate AND (ValidTo IS NULL OR ValidTo >= Period.StartDate)`.

(Previously: SQL used `WHERE bl."PeriodId" = @PeriodId`; revision join ordered by `RevisedAt DESC`)

#### Scenario: BudgetLine active for period included in totals `@integration`
- GIVEN BudgetLine.StartDate=2025-01-01, Period.StartDate=2025-03-01
- WHEN GET `/budgets/{budgetId}/periods/{periodId}/execution-totals`
- THEN that BudgetLine appears in totals with amount from revision effective on 2025-03-01

#### Scenario: BudgetLine not covering period excluded from totals `@integration`
- GIVEN BudgetLine.StartDate=2025-06-01, Period.StartDate=2025-03-01
- WHEN GET execution-totals
- THEN that BudgetLine does not appear in totals

#### Scenario: Correct revision amount selected for period `@integration`
- GIVEN a BudgetLine with revisions: [2025-01-01, 2025-05-31, 1000] and [2025-06-01, null, 2000]; Period.StartDate=2025-06-15
- WHEN GET execution-totals
- THEN BudgetedAmount = 2000 (second revision is effective)

---

### REMOVED: REQ-EXEC-7 (old PERIOD_MISMATCH code)

(Reason: BudgetLine.PeriodId no longer exists; replaced by date-range coverage check above)
(Migration: Error code `PERIOD_MISMATCH` is replaced by `BUDGET_LINE_NOT_IN_PERIOD`)

---

## Delta: budget-structure-ui

### MODIFIED: REQ-BL-1 — BudgetLine List

The view MUST load all lines for a Budget via `GET /api/budgets/{budgetId}/lines` (no `periodId`). Displayed fields MUST include `startDate`, `endDate`, and current `budgetedAmount`. The `isRecurring` field MUST NOT be displayed.

(Previously: loaded via `/periods/{periodId}/lines`; showed `isRecurring`)

#### Scenario: Budget line view loads at budget level `@unit`
- GIVEN user navigates to the budget lines view
- WHEN the view mounts
- THEN `GET /api/budgets/{budgetId}/lines` is called (no periodId)
- AND rows show startDate and endDate columns; no isRecurring column

---

### MODIFIED: REQ-BL-2 — Inline BudgetLine Creation

Create payload MUST include `startDate` (required), `endDate` (optional), `initialAmount` (required), `currencyId`. MUST NOT include `isRecurring` or `periodId`. Endpoint is `POST /api/budgets/{budgetId}/lines`.

(Previously: included `isRecurring`; called `/periods/{periodId}/lines`)

#### Scenario: Operator creates budget line inline with dates `@unit`
- GIVEN an operator user on the budget line view with startDate=2025-01-01 and initialAmount=1000
- WHEN they submit the inline row
- THEN `POST /api/budgets/{budgetId}/lines` is called with startDate, initialAmount, currencyId; no isRecurring field

#### Scenario: Missing startDate blocks inline submission `@unit`
- GIVEN startDate is empty
- WHEN the user attempts to submit
- THEN submission is blocked; startDate validation error shown

---

### MODIFIED: REQ-BL-3 — BudgetLine Edit via Modal

The edit modal MUST show `startDate` and `endDate` date inputs. For amount revision: MUST show `validFrom` (required, min = today) and `validTo` (optional) fields when editing amount. MUST NOT show `isRecurring`. Update endpoint is `PUT /api/budgets/{budgetId}/lines/{lineId}`.

(Previously: modal showed `isRecurring` checkbox; no validFrom/validTo fields; route included periodId)

#### Scenario: Edit modal shows date range fields `@unit`
- GIVEN the edit modal opens for an existing BudgetLine
- WHEN the modal renders
- THEN startDate and endDate inputs are present; isRecurring field is absent

#### Scenario: Amount change requires validFrom `@unit`
- GIVEN the operator changes the amount in the edit modal
- WHEN validFrom is empty
- THEN submission is blocked; validFrom required error shown

#### Scenario: validFrom before today blocked `@unit`
- GIVEN validFrom = yesterday's date
- WHEN the operator attempts to submit
- THEN submission is blocked (no retroactive splits)

---

### MODIFIED: REQ-BL-4 — BudgetLine Delete

Delete endpoint MUST be `DELETE /api/budgets/{budgetId}/lines/{lineId}` (no `periodId`).

(Previously: route included `periodId`)

#### Scenario: Operator deletes budget line via new route `@unit`
- GIVEN the operator confirms deletion
- THEN `DELETE /api/budgets/{budgetId}/lines/{lineId}` is called (no periodId in URL)

---

### MODIFIED: REQ-RESTORE-1 — Restore BudgetLine

Restore endpoint MUST be `POST /api/budgets/{budgetId}/lines/{lineId}/restore` (no `periodId`).

(Previously: route was `/periods/{periodId}/budget-lines/{lineId}/restore`)

#### Scenario: Restore BudgetLine via new route `@unit`
- GIVEN show-deleted toggle ON and a soft-deleted BudgetLine
- WHEN the operator clicks "Restore"
- THEN `POST /api/budgets/{budgetId}/lines/{lineId}/restore` is called (no periodId)

---

### MODIFIED: REQ-RESTORE-PERIOD-1 — Period Restore Disclosure (BudgetLines removed)

The disclosure MUST be removed: Period restore no longer cascades to BudgetLines.

(Previously: disclosure warned "Restoring this Period will also restore all its BudgetLines")

#### Scenario: Period restore proceeds without disclosure `@unit`
- GIVEN show-deleted toggle ON and a soft-deleted Period
- WHEN the admin clicks "Restore"
- THEN no BudgetLine cascade disclosure is shown; restore proceeds directly after standard confirmation

---

### ADDED: REQ-BL-MATRIX-1 — BudgetMatrix Date-Coverage Cell Gating

The BudgetMatrixView MUST gate each cell per-period using date-range coverage. A BudgetLine is considered active for a period column when `BudgetLine.startDate <= period.startDate AND (BudgetLine.endDate == null OR BudgetLine.endDate >= period.startDate)`. Inactive cells MUST show 0 and MUST disable execution actions.

#### Scenario: Active cell — BudgetLine covers the period column `@unit`
- GIVEN BudgetLine.startDate=2025-01-01, endDate=null; period.startDate=2025-06-01
- WHEN the matrix renders that cell
- THEN the cell shows the budgeted amount and execution actions are enabled

#### Scenario: Inactive cell — BudgetLine does not cover the period column `@unit`
- GIVEN BudgetLine.startDate=2025-06-01; period.startDate=2025-03-01
- WHEN the matrix renders that cell
- THEN the cell shows 0 and execution actions are disabled

---

### ADDED: REQ-BL-STORE-1 — budgetStructure Store Scoped to Budget

All BudgetLine store actions (`loadLines`, `createLine`, `updateLine`, `deleteLine`, `restoreLine`) MUST be scoped to `budgetId` only. The `periodId` parameter MUST be removed from all five actions. State MUST be keyed by `budgetId`.

#### Scenario: loadLines called without periodId `@unit`
- GIVEN the BudgetLinesView mounts for budgetId=B1
- WHEN `loadLines(B1)` is called
- THEN the API call is `GET /api/budgets/B1/lines` (no periodId)

---

### MODIFIED: REQ-I18N-1 — Budget Structure i18n Keys (BudgetLine additions)

The following i18n keys MUST be added to both `en.json` and `es.json`:
- `budgetStructure.budgetLines.validation.startDateRequired`
- `budgetStructure.budgetLines.validation.endDateAfterStartDate`
- `budgetStructure.budgetLines.validation.validFromRequired`
- `budgetStructure.budgetLines.validation.validFromNotInPast`
- `budgetStructure.budgetLines.validation.validFromOutOfRange`

The following keys MUST be removed from both locale files:
- Any key referencing `isRecurring` for BudgetLine forms

(Previously: no date-range validation keys for BudgetLine; isRecurring keys may exist)

#### Scenario: New date-range keys resolve in both locales `@unit`
- GIVEN locale is "en" or "es"
- WHEN inline validation triggers on startDate or validFrom in the BudgetLine form
- THEN a translated error message is shown using the new keys

---

## Validation Error Codes Added/Changed

| Code | Trigger | HTTP |
|---|---|---|
| `BUDGET_LINE_NOT_IN_PERIOD` | BudgetLine date range does not cover the target Period | 422 |

| Code | Removed | Replaced by |
|---|---|---|
| `PERIOD_MISMATCH` | BudgetLine.PeriodId no longer exists | `BUDGET_LINE_NOT_IN_PERIOD` |
