# Delta Spec: budget-line-customizations

**Change**: `budget-line-customizations`
**Date**: 2026-07-21
**Domains**: `budget-line-revisions` (new), `budget-structure` (delta), `budget-execution` (delta)

---

## Capability Index

| Capability | Type | Requirements |
|---|---|---|
| `budget-line-revisions` | New | REQ-BLR-01 … REQ-BLR-05 |
| `budget-structure` | Modified | REQ-BL-DATERANGE-1, REQ-BL-CONCURRENCY-1, REQ-BL-AUDIT-1 |
| `budget-execution` | Modified | REQ-EXEC-RESTORE-DATERANGE-1 |

---

## New Capability: budget-line-revisions

### Requirement: REQ-BLR-01 — List BudgetLine Revisions

The system MUST expose `GET /api/budgets/{budgetId}/lines/{lineId}/revisions` returning all revisions for the specified BudgetLine ordered by `ValidFrom` ascending. Requires `budget:admin`.

#### Scenario: Happy path — returns all revisions `@integration`
- GIVEN a BudgetLine with 3 revisions and caller has `budget:admin`
- WHEN GET `.../lines/{lineId}/revisions`
- THEN HTTP 200; array of 3 items ordered by `ValidFrom` ASC; each item includes `id`, `validFrom`, `validTo`, `amount`, `currencyId`, `note`

#### Scenario: BudgetLine not found `@integration`
- GIVEN lineId does not exist under the given budgetId
- WHEN GET `.../lines/{lineId}/revisions`
- THEN HTTP 404

#### Scenario: Unauthenticated rejected `@integration`
- GIVEN no JWT in the request
- WHEN GET `.../lines/{lineId}/revisions`
- THEN HTTP 401

#### Scenario: Insufficient role rejected `@integration`
- GIVEN caller has `budget:operator` (below `budget:admin`)
- WHEN GET `.../lines/{lineId}/revisions`
- THEN HTTP 403

---

### Requirement: REQ-BLR-02 — Create BudgetLine Revision

The system MUST expose `POST /api/budgets/{budgetId}/lines/{lineId}/revisions` to add a revision by delegating to `BudgetLine.SplitRevision()`. Requires `budget:admin`. The request MUST supply `validFrom` (DateOnly), `newAmount` (decimal > 0), `currencyId` (Guid), and optionally `note` (varchar 200 max). `validFrom` MUST be today or in the future and MUST fall within the BudgetLine's `[StartDate, EndDate]` range.

#### Scenario: Happy path — revision created `@integration`
- GIVEN an active BudgetLine, caller has `budget:admin`, `validFrom` is today, `newAmount` = 2000
- WHEN POST `.../lines/{lineId}/revisions`
- THEN HTTP 201; existing enclosing revision trimmed; new revision inserted; gapless chain preserved

#### Scenario: `validFrom` before today rejected `@unit`
- GIVEN `validFrom` = yesterday
- WHEN validator runs
- THEN HTTP 422 (no retroactive splits)

#### Scenario: `validFrom` outside BudgetLine date range rejected `@unit`
- GIVEN BudgetLine.EndDate = 2025-12-31, `validFrom` = 2026-01-01
- WHEN validator runs
- THEN HTTP 422

#### Scenario: `newAmount` zero rejected `@unit`
- GIVEN `newAmount` = 0
- WHEN validator runs
- THEN HTTP 422

#### Scenario: Concurrent modification returns 409 `@integration`
- GIVEN the BudgetLine's `xmin` token is stale (concurrent mutation)
- WHEN POST `.../lines/{lineId}/revisions`
- THEN HTTP 409 Conflict

---

### Requirement: REQ-BLR-03 — Delete BudgetLine Revision

The system MUST expose `DELETE /api/budgets/{budgetId}/lines/{lineId}/revisions/{revisionId}`. Deletion MUST call `BudgetLine.DeleteRevision(revisionId)`, which performs gapless repair. Requires `budget:admin`.

**Deletion rules:**
- If the revision has a preceding revision: the preceding revision's `ValidTo` MUST be extended to cover the deleted revision's range (if deleted revision is the last, preceding `ValidTo` becomes `null`).
- If the revision has no preceding revision (original): the delete MUST be rejected with `CANNOT_DELETE_ORIGINAL_REVISION`.
- If any active (non-soft-deleted) execution record's period falls within the revision's `[ValidFrom, ValidTo]` range: the delete MUST be rejected with `REVISION_HAS_ACTIVE_EXECUTIONS`.
- Soft-deleted execution records MUST NOT block deletion.

#### Scenario: Happy path — middle revision deleted with gapless repair `@unit`
- GIVEN revisions R1=[2025-01-01, 2025-05-31], R2=[2025-06-01, 2025-08-31], R3=[2025-09-01, null]
- WHEN DELETE `.../revisions/{R2.id}`
- THEN R1.ValidTo = 2025-08-31; R3 unchanged; R2 removed

#### Scenario: Happy path — last revision deleted, preceding becomes open-ended `@unit`
- GIVEN revisions R1=[2025-01-01, 2025-05-31], R2=[2025-06-01, null]
- WHEN DELETE `.../revisions/{R2.id}`
- THEN R1.ValidTo = null; R2 removed

#### Scenario: Original revision blocked `@unit`
- GIVEN the revision has no preceding revision
- WHEN DELETE `.../revisions/{revisionId}`
- THEN HTTP 422, error code `CANNOT_DELETE_ORIGINAL_REVISION`

#### Scenario: Active executions in range blocked `@integration`
- GIVEN revision [2025-06-01, 2025-08-31] AND an active execution record in a period within Jun–Aug 2025
- WHEN DELETE `.../revisions/{revisionId}`
- THEN HTTP 409, error code `REVISION_HAS_ACTIVE_EXECUTIONS`

#### Scenario: Soft-deleted executions do NOT block `@integration`
- GIVEN revision [2025-06-01, 2025-08-31] AND only soft-deleted execution records in that range
- WHEN DELETE `.../revisions/{revisionId}`
- THEN HTTP 204; gapless repair applied

#### Scenario: Audit entry written on successful delete `@integration`
- GIVEN a valid revision delete
- WHEN DELETE `.../revisions/{revisionId}` returns 204
- THEN an AuditLog entry exists with `Action = "BudgetLineRevisionDeleted"`, `EntityId = revisionId`

#### Scenario: Concurrent modification returns 409 `@integration`
- GIVEN the BudgetLine's `xmin` token is stale
- WHEN DELETE `.../revisions/{revisionId}`
- THEN HTTP 409 Conflict

---

### Requirement: REQ-BLR-04 — i18n Error Keys for Revision Errors

The system MUST provide EN and ES translations for all revision-related error codes.

| Error Code | i18n Key |
|---|---|
| `CANNOT_DELETE_ORIGINAL_REVISION` | `budgetLineRevisions.errors.cannotDeleteOriginalRevision` |
| `REVISION_HAS_ACTIVE_EXECUTIONS` | `budgetLineRevisions.errors.revisionHasActiveExecutions` |
| `RANGE_WOULD_ORPHAN_REVISION` | `budgetLineRevisions.errors.rangeWouldOrphanRevision` |
| `RANGE_WOULD_ORPHAN_EXECUTION` | `budgetLineRevisions.errors.rangeWouldOrphanExecution` |
| `EXECUTION_OUT_OF_DATE_RANGE` | `budgetLineRevisions.errors.executionOutOfDateRange` |

#### Scenario: All error keys present in both locales `@unit`
- GIVEN `en.json` and `es.json` are loaded
- WHEN each error code key is looked up
- THEN a non-empty translated string is returned in each locale

---

### Requirement: REQ-BLR-05 — Frontend Customizations View

The frontend MUST provide a `BudgetLineCustomizationsView` accessible at child route `lines/:lineId/customizations`. The view MUST display a revision table showing `ValidFrom`, `ValidTo`, `Amount`, `Currency`, and `Note` columns, with inline create and delete actions. The `BudgetLineModal` edit mode MUST NOT render the Amount Revision section (validFrom, validTo, newAmount fields).

#### Scenario: Navigation to customizations view `@e2e`
- GIVEN the user is on the BudgetLines list and clicks the customizations link for a line
- WHEN the router resolves `lines/:lineId/customizations`
- THEN `BudgetLineCustomizationsView` renders the revision table for that line

#### Scenario: BudgetLineModal edit mode has no Amount Revision section `@unit`
- GIVEN `BudgetLineModal` is opened in edit mode
- WHEN the modal renders
- THEN no `validFrom`, `validTo`, or `newAmount` fields are present in the DOM

#### Scenario: Create revision from customizations view `@integration`
- GIVEN the customizations view is open
- WHEN the user fills the inline create form with `validFrom`, `newAmount`, `currencyId` and submits
- THEN POST `.../revisions` is called and the table refreshes with the new revision

#### Scenario: Delete revision from customizations view `@integration`
- GIVEN the customizations view displays a non-original revision
- WHEN the user clicks delete on that revision
- THEN DELETE `.../revisions/{revisionId}` is called and the row is removed from the table

---

## Delta: budget-structure (Modified)

### Requirement: REQ-BL-DATERANGE-1 — UpdateDateRange Domain Method and Endpoint

The system MUST expose `PATCH /api/budgets/{budgetId}/lines/{lineId}/date-range` to update a BudgetLine's `StartDate` and/or `EndDate`. The domain method `BudgetLine.UpdateDateRange(startDate, endDate)` MUST reject the change if it would orphan existing revisions or active execution records.

**Guards (checked in order):**
1. If any revision's `ValidFrom` or `ValidTo` falls outside `[startDate, endDate]` → reject with `RANGE_WOULD_ORPHAN_REVISION`.
2. If any active (non-soft-deleted) execution record's period falls outside `[startDate, endDate]` → reject with `RANGE_WOULD_ORPHAN_EXECUTION`.
3. Soft-deleted execution records MUST NOT block the update.

Requires `budget:admin`. On success, writes an AuditLog entry with `Action = "BudgetLineDateRangeUpdated"`, `EntityId = budgetLineId`.

#### Scenario: Happy path — range shrunk without orphaning `@integration`
- GIVEN BudgetLine [2025-01-01, null], single revision [2025-01-01, null], no executions outside new range
- WHEN PATCH `.../date-range` with StartDate=2025-01-01, EndDate=2025-12-31
- THEN HTTP 200; BudgetLine.EndDate = 2025-12-31; audit entry written

#### Scenario: Shrink orphans a revision `@unit`
- GIVEN revision ValidTo=2025-12-31 AND new EndDate=2025-06-30
- WHEN PATCH `.../date-range`
- THEN HTTP 422, error code `RANGE_WOULD_ORPHAN_REVISION`

#### Scenario: Shrink orphans an active execution `@integration`
- GIVEN active execution in a period with StartDate=2025-11-01 AND new EndDate=2025-06-30
- WHEN PATCH `.../date-range`
- THEN HTTP 409, error code `RANGE_WOULD_ORPHAN_EXECUTION`

#### Scenario: Soft-deleted executions outside new range do NOT block `@integration`
- GIVEN only soft-deleted execution records outside new range
- WHEN PATCH `.../date-range`
- THEN HTTP 200; update succeeds

#### Scenario: Concurrent modification returns 409 `@integration`
- GIVEN stale `xmin` token
- WHEN PATCH `.../date-range`
- THEN HTTP 409 Conflict

#### Scenario: Audit log entry on success `@integration`
- GIVEN a valid date-range update
- WHEN PATCH `.../date-range` returns 200
- THEN AuditLog contains entry with `Action = "BudgetLineDateRangeUpdated"`, `EntityId = budgetLineId`

---

### Requirement: REQ-BL-CONCURRENCY-1 — BudgetLine Optimistic Concurrency via xmin

`BudgetLine` MUST use PostgreSQL `xmin` as an EF Core concurrency token (shadow property). Any concurrent mutation detected by EF Core (`DbUpdateConcurrencyException`) MUST be caught by the handler and returned as HTTP 409 Conflict.

#### Scenario: Concurrent write on BudgetLine detected `@integration`
- GIVEN two concurrent requests mutating the same BudgetLine
- WHEN the second request is processed after the first committed
- THEN HTTP 409 Conflict is returned for the second request

#### Scenario: Sequential writes succeed `@integration`
- GIVEN a mutation on BudgetLine followed by a fresh load and another mutation
- WHEN the second request carries the updated `xmin`
- THEN HTTP 200 (or 204) is returned

---

### Requirement: REQ-BL-AUDIT-1 — Audit Log for BudgetLine Mutations

The system MUST write AuditLog entries for the following domain events on BudgetLine:

| Event | Action value | EntityId |
|---|---|---|
| Date-range changed | `"BudgetLineDateRangeUpdated"` | `budgetLineId` |
| Revision deleted | `"BudgetLineRevisionDeleted"` | `revisionId` |

AuditLog writes MUST use the existing `AuditLog` table and write helper. Each entry MUST include `UserId` and `Timestamp`.

#### Scenario: Date-range audit entry fields `@integration`
- GIVEN a successful `PATCH .../date-range`
- WHEN AuditLog is queried
- THEN an entry exists with Action=`"BudgetLineDateRangeUpdated"`, EntityId=budgetLineId, UserId set, Timestamp ≈ now

#### Scenario: Revision delete audit entry fields `@integration`
- GIVEN a successful `DELETE .../revisions/{revisionId}`
- WHEN AuditLog is queried
- THEN an entry exists with Action=`"BudgetLineRevisionDeleted"`, EntityId=revisionId, UserId set, Timestamp ≈ now

---

## Delta: budget-execution (Modified)

### Requirement: REQ-EXEC-RESTORE-DATERANGE-1 — Restore ExecutionRecord BudgetLine Date-Range Guard

`RestoreExecutionRecord` MUST load the parent BudgetLine and verify that the execution record's Period date range (`Period.StartDate` and `Period.EndDate`) falls within the BudgetLine's current `[StartDate, EndDate]`. If the Period falls outside the BudgetLine range, the restore MUST be rejected with error code `EXECUTION_OUT_OF_DATE_RANGE` (422). Soft-deleted execution records that pass this guard MUST be restored normally. `OperationDate` MUST NOT be used for this check; only Period dates apply.

(Previously: `RestoreExecutionRecord` only checked `Period.IsClosed` — no BudgetLine date-range intersection check existed.)

#### Scenario: Happy path — period within BudgetLine range `@integration`
- GIVEN soft-deleted ExecutionRecord; Period.StartDate=2025-03-01, Period.EndDate=2025-03-31; BudgetLine.StartDate=2025-01-01, BudgetLine.EndDate=null
- WHEN POST `.../executions/{id}/restore`
- THEN HTTP 200; ExecutionRecord.DeletedAt = null

#### Scenario: Period starts before BudgetLine start — rejected `@integration`
- GIVEN Period.StartDate=2024-12-01; BudgetLine.StartDate=2025-01-01
- WHEN POST `.../executions/{id}/restore`
- THEN HTTP 422, error code `EXECUTION_OUT_OF_DATE_RANGE`

#### Scenario: Period ends after BudgetLine end — rejected `@integration`
- GIVEN Period.EndDate=2026-01-31; BudgetLine.EndDate=2025-12-31
- WHEN POST `.../executions/{id}/restore`
- THEN HTTP 422, error code `EXECUTION_OUT_OF_DATE_RANGE`

#### Scenario: OperationDate outside BudgetLine range does NOT block `@integration`
- GIVEN Period within BudgetLine range; OperationDate outside BudgetLine range
- WHEN POST `.../executions/{id}/restore`
- THEN HTTP 200 (Period dates are authoritative, not OperationDate)

#### Scenario: Period.IsClosed still blocks restore `@integration`
- GIVEN Period.IsClosed = true AND Period within BudgetLine date range
- WHEN POST `.../executions/{id}/restore`
- THEN HTTP 409, error code `PERIOD_CLOSED`

---

## Validation Error Code Summary

| Code | Domain | HTTP | Trigger |
|---|---|---|---|
| `RANGE_WOULD_ORPHAN_REVISION` | budget-structure | 422 | Date-range shrink orphans a revision |
| `RANGE_WOULD_ORPHAN_EXECUTION` | budget-structure | 409 | Date-range shrink has active executions outside |
| `REVISION_HAS_ACTIVE_EXECUTIONS` | budget-line-revisions | 409 | Revision delete blocked by active executions |
| `CANNOT_DELETE_ORIGINAL_REVISION` | budget-line-revisions | 422 | Delete of original (no predecessor) revision |
| `EXECUTION_OUT_OF_DATE_RANGE` | budget-execution | 422 | Restore blocked — period outside BudgetLine range |

---

## RBAC Summary

| Operation | Required Role |
|---|---|
| GET `.../revisions` | `budget:admin` |
| POST `.../revisions` | `budget:admin` |
| DELETE `.../revisions/{id}` | `budget:admin` |
| PATCH `.../date-range` | `budget:admin` |
| POST `.../executions/{id}/restore` | `budget:operator` (unchanged) |

---

## Assumptions

1. `RANGE_WOULD_ORPHAN_EXECUTION` returns HTTP 409 (conflict) because active executions are a runtime data conflict, while `RANGE_WOULD_ORPHAN_REVISION` returns HTTP 422 (model validation). The proposal lists both as domain errors without specifying HTTP code per error; this split is inferred from the nature of each guard.
2. The revision delete guard checks whether any active execution record's **period** date range overlaps the revision's `[ValidFrom, ValidTo]` — not the execution record's `OperationDate`. This parallels the restore guard's use of Period dates.
3. Frontend date-range editing UI (PATCH `.../date-range` form) is out of scope for PR1 and PR2. PR2 delivers the backend endpoint only; the frontend form is deferred.
4. `BudgetLinesView.vue` requires a nav link per row pointing to `lines/:lineId/customizations` (referenced in explore.md) — this is included under REQ-BLR-05 frontend scope.
