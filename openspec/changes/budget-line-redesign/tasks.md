# Tasks: Budget Line Redesign

Artifact store: hybrid
Change: budget-line-redesign
Branch: feat/budget-line-redesign
Chain strategy: feature-branch-chain (PR1→PR2→PR3; PR4 parallel with PR3)
Delivery: ask-on-risk (stop if any PR approaches 400 lines)

## Dependency Graph

```
PR1 (entity + EF + migration)
  └── PR2 (backend slices)
        ├── PR3 (frontend)     ← targets PR2 branch; parallel with PR4
        └── PR4 (integration tests) ← targets PR2 branch; parallel with PR3
```

---

## PR1 — Entity, EF Configuration, Migration Wipe

**Branch**: `feat/budget-line-redesign` (tracker / PR1 lands here)
**Target**: tracker branch
**Est. lines**: ~300
**Dependencies**: none

### PR1-T1 — Modify `BudgetLineRevision` entity

**Files**:
- `src/MyBudget.Features/SharedKernel/Entities/BudgetLineRevision.cs`

**What to implement**:
- Remove `RevisedAt` (DateTimeOffset) field and constructor parameter.
- Add `ValidFrom` (DateOnly, required) and `ValidTo` (DateOnly?, nullable).
- Add factory method `BudgetLineRevision.Create(budgetId, lineId, amount, currencyId, validFrom, validTo?, note?)`.
- Add `SetValidTo(DateOnly? value)` mutator (used by `SplitRevision`).

**Tests to write** (`tests/MyBudget.Features.Tests/BudgetLineRevisionTests.cs`):
- `Create_SetsValidFromAndValidTo_Correctly` — factory sets both date fields.
- `SetValidTo_UpdatesField` — mutator persists value.

**Spec refs**: REQ-BL-REVISION-1

---

### PR1-T2 — Modify `BudgetLine` entity + `SplitRevision` domain method

**Files**:
- `src/MyBudget.Features/SharedKernel/Entities/BudgetLine.cs`

**What to implement**:
- Remove `PeriodId` (Guid), `IsRecurring` (bool), `Period` navigation property.
- Add `StartDate` (DateOnly, required), `EndDate` (DateOnly?, nullable).
- Update `BudgetLine.Create(budgetId, ..., startDate, endDate)` factory.
- Update `BudgetLine.Update(name, categoryGroupId, categoryId)` — metadata-only, no date/amount params.
- Implement `SplitRevision(newValidFrom, newValidTo, amount, currencyId)`:
  1. Guard: `newValidFrom < DateOnly.FromDateTime(DateTime.UtcNow)` → throw `InvalidOperationException`.
  2. **Edge Case B guard**: if `newValidFrom == enclosing.ValidFrom` → overwrite the enclosing revision's amount/currencyId in-place (no split, no trimming).
  3. Else: find enclosing revision where `ValidFrom <= newValidFrom AND (ValidTo is null OR ValidTo >= newValidFrom)`.
  4. If none found → throw `InvalidOperationException("No enclosing revision found.")`.
  5. Trim enclosing: `enclosing.SetValidTo(newValidFrom.AddDays(-1))`.
  6. Insert new revision `[newValidFrom, newValidTo]`.
  7. If `newValidTo.HasValue AND (originalValidTo is null OR originalValidTo > newValidTo)` → insert tail revision `[newValidTo.AddDays(1), originalValidTo]`.

**Tests to write** (`tests/MyBudget.Features.Tests/BudgetLineTests.cs`):
- `Create_SetsStartDateAndEndDate` — factory sets fields.
- `SplitRevision_HeadNewTail_CreatesThreeRevisions` — REQ-BL-SPLIT-1 happy path.
- `SplitRevision_OpenEnded_CreatesTwoRevisions` — open-ended split produces no tail.
- `SplitRevision_NoEnclosingRevision_ThrowsException` — REQ-BL-SPLIT-1 error scenario.
- `SplitRevision_RetroactiveDate_ThrowsException` — validFrom < today rejected.
- `SplitRevision_ExactBoundary_OverwritesInPlace` — **Edge Case B**: newValidFrom == enclosing.ValidFrom replaces amount in-place without splitting.

**Spec refs**: REQ-BL-ENTITY-1, REQ-BL-SPLIT-1

---

### PR1-T3 — Remove `BudgetLines` navigation from `Period` entity

**Files**:
- `src/MyBudget.Features/SharedKernel/Entities/Period.cs`

**What to implement**:
- Remove `ICollection<BudgetLine> BudgetLines` navigation property and any related `AddBudgetLine()` helper.

**Tests**: none required — covered by compilation + EF config in PR1-T4.

**Spec refs**: MODIFIED: REQ-CYC-03 (cascade removed)

---

### PR1-T4 — Update EF configurations

**Files**:
- `src/MyBudget.Features/Persistence/Configurations/BudgetLineConfiguration.cs`
- `src/MyBudget.Features/Persistence/Configurations/BudgetLineRevisionConfiguration.cs`

**What to implement** (`BudgetLineConfiguration`):
- Remove `PeriodId` FK, its index, and cascade delete configuration.
- Add `StartDate` (DateOnly, required) and `EndDate` (DateOnly?, nullable) column mappings.
- Add unique index: `UX_BudgetLines_BudgetId_Name` on `(BudgetId, Name)` — no filter clause (includes soft-deleted rows; SQLite does not support partial indexes).

**What to implement** (`BudgetLineRevisionConfiguration`):
- Remove `RevisedAt` column mapping and any index on it.
- Add `ValidFrom` (DateOnly, required) and `ValidTo` (DateOnly?, nullable) column mappings.
- Add index: `IX_BudgetLineRevisions_BudgetLineId_ValidFrom` on `(BudgetLineId, ValidFrom)`.

**Tests**: EF schema validated implicitly by migration in PR1-T5 and by unit tests using `UseSqlite(":memory:")`.

**Spec refs**: REQ-BL-ENTITY-1, REQ-BL-REVISION-1, REQ-BL-NAME-1 (unique index)

---

### PR1-T5 — Migration wipe and fresh `InitialCreate`

**Files**:
- `src/MyBudget.Features/Persistence/Migrations/` — delete all existing migration files.
- New: `src/MyBudget.Features/Persistence/Migrations/<timestamp>_InitialCreate.cs` + snapshot.

**What to implement**:
- Run `dotnet ef database drop` (dev environment only).
- Delete all files under `Migrations/`.
- Run `dotnet ef migrations add InitialCreate`.
- Verify `dotnet ef database update` succeeds against a fresh SQLite file.

**Tests**: smoke — EF `db.Database.EnsureCreated()` in existing unit test base must succeed (validates schema).

**Spec refs**: Design decision #7 (wipe strategy)

---

**PR1 Commit sequence** (work-unit commits):
1. `feat(entities): update BudgetLineRevision — ValidFrom/ValidTo, remove RevisedAt`
2. `feat(entities): update BudgetLine — StartDate/EndDate, SplitRevision domain method`
3. `feat(entities): remove BudgetLines nav from Period`
4. `feat(ef): update BudgetLine/Revision EF configurations`
5. `feat(migration): wipe all migrations, add fresh InitialCreate`

---

## PR2 — Backend Slices

**Branch**: `feat/budget-line-redesign-pr2`
**Target**: `feat/budget-line-redesign` (PR1 branch)
**Est. lines**: ~350
**Dependencies**: PR1 merged into tracker

> Watch: if backend execution slices push this over 400 lines, split into PR2a (BudgetStructure slices) and PR2b (BudgetExecution slices). Evaluate after implementing PR2-T1 through PR2-T6.

### PR2-T1 — `CreateBudgetLine` slice

**Files** (4-file slice):
- `Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineCommand.cs`
- `Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineValidator.cs`
- `Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineHandler.cs`
- `Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineEndpoint.cs`

**What to implement**:
- Command: remove `PeriodId`, `IsRecurring`; add `StartDate` (DateOnly), `EndDate` (DateOnly?), `InitialAmount` (decimal), `CurrencyId` (Guid).
- Validator: `StartDate` required; `EndDate > StartDate` when provided; `InitialAmount > 0`; uniqueness validation deferred to handler (DB constraint).
- Handler: check `UNIQUE(BudgetId, Name)` — if conflict → `Result.Fail("BUDGET_LINE_NAME_DUPLICATE")`; call `BudgetLine.Create(...)` + `BudgetLineRevision.Create(...)` with `[StartDate, EndDate]`; `SaveChangesAsync`; catch `DbUpdateException` for unique constraint violation.
- Endpoint: `POST /api/budgets/{budgetId}/lines`.

**Tests to write** (`tests/MyBudget.Features.Tests/CreateBudgetLineHandlerTests.cs`):
- `Handle_ValidCommand_CreatesLineAndRevision`.
- `Handle_DuplicateName_ReturnsDuplicateError` (REQ-BL-NAME-1).
- `Handle_SameNameDifferentBudget_Succeeds` (REQ-BL-NAME-1 scope).
- `Handle_EndDateBeforeStartDate_ValidationFails` (REQ-BL-02 unit).
- `Handle_ZeroAmount_ValidationFails` (REQ-BL-02 unit).
- `Handle_NullEndDate_CreatesPerpetuaRevision` (REQ-BL-02 open-ended).

**Spec refs**: REQ-BL-02, REQ-BL-NAME-1

---

### PR2-T2 — `UpdateBudgetLine` slice + IsClosed guard

**Files** (4-file slice):
- `Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineCommand.cs`
- `Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineValidator.cs`
- `Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineHandler.cs`
- `Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineEndpoint.cs`

**What to implement**:
- Command: remove `PeriodId`, `IsRecurring`; retain `Name`, `CategoryGroupId`, `CategoryId`; add optional `ValidFrom` (DateOnly?), `ValidTo` (DateOnly?), `NewAmount` (decimal?), `CurrencyId` (Guid?).
- Validator: `ValidFrom >= today` when provided; `ValidFrom` within BudgetLine `[StartDate, EndDate]` range; `NewAmount > 0` when provided; if `NewAmount` provided then `ValidFrom` required.
- Handler:
  1. Load BudgetLine with revisions.
  2. Apply metadata update (`name`, `categoryGroupId`, `categoryId`).
  3. If `NewAmount` provided:
     - **Edge Case A — IsClosed guard**: query `Periods WHERE BudgetId = @budgetId AND StartDate <= @validFrom AND EndDate >= @validFrom AND IsClosed = true`; if any match → return `Result.Fail("PERIOD_CLOSED")` with HTTP 409.
     - Call `line.SplitRevision(validFrom, validTo, newAmount, currencyId)`.
  4. `SaveChangesAsync`.
- Endpoint: `PUT /api/budgets/{budgetId}/lines/{lineId}`.

**Tests to write** (`tests/MyBudget.Features.Tests/UpdateBudgetLineHandlerTests.cs`):
- `Handle_MetadataOnlyUpdate_RevisionCountUnchanged` (REQ-BL-03 metadata-only).
- `Handle_AmountChange_SplitsRevision` (REQ-BL-03 split).
- `Handle_ValidFromInClosedPeriod_ReturnsPeriodClosed` (**Edge Case A**).
- `Handle_ValidFromNotInClosedPeriod_Succeeds` (closed period elsewhere in budget does not block).
- `Handle_ValidFromBeforeToday_ValidationFails` (REQ-BL-03 retroactive).
- `Handle_ValidFromOutsideLineRange_ValidationFails` (REQ-BL-03 out-of-range).
- `Handle_AmountWithoutValidFrom_ValidationFails`.
- `Handle_MetadataUpdateAllPeriodsClosedBudget_Succeeds` (REQ-BL-01 metadata-only allowed).

**Spec refs**: REQ-BL-01, REQ-BL-03

---

### PR2-T3 — `DeleteBudgetLine` slice

**Files** (3 files — no separate validator needed):
- `Features/BudgetStructure/DeleteBudgetLine/DeleteBudgetLineCommand.cs`
- `Features/BudgetStructure/DeleteBudgetLine/DeleteBudgetLineHandler.cs`
- `Features/BudgetStructure/DeleteBudgetLine/DeleteBudgetLineEndpoint.cs`

**What to implement**:
- Command: remove `PeriodId`; keep `BudgetId`, `LineId`.
- Handler: soft-delete (`DeletedAt = now`); no IsClosed guard.
- Endpoint: `DELETE /api/budgets/{budgetId}/lines/{lineId}`.

**Tests to write** (`tests/MyBudget.Features.Tests/DeleteBudgetLineHandlerTests.cs`):
- `Handle_ActiveLine_SoftDeletes` (REQ-BL-04).

**Spec refs**: REQ-BL-04

---

### PR2-T4 — `RestoreBudgetLine` slice

**Files**:
- `Features/BudgetStructure/RestoreBudgetLine/RestoreBudgetLineCommand.cs`
- `Features/BudgetStructure/RestoreBudgetLine/RestoreBudgetLineHandler.cs`
- `Features/BudgetStructure/RestoreBudgetLine/RestoreBudgetLineEndpoint.cs`

**What to implement**:
- Command: remove `PeriodId`; keep `BudgetId`, `LineId`.
- Handler: clear `DeletedAt`.
- Endpoint: `POST /api/budgets/{budgetId}/lines/{lineId}/restore`.

**Tests to write** (`tests/MyBudget.Features.Tests/RestoreBudgetLineHandlerTests.cs`):
- `Handle_SoftDeletedLine_ClearsDeletedAt` (REQ-RST-05).

**Spec refs**: REQ-RST-05

---

### PR2-T5 — `ListBudgetLines` slice

**Files**:
- `Features/BudgetStructure/ListBudgetLines/ListBudgetLinesQuery.cs`
- `Features/BudgetStructure/ListBudgetLines/ListBudgetLinesHandler.cs`
- `Features/BudgetStructure/ListBudgetLines/ListBudgetLinesEndpoint.cs`

**What to implement**:
- Query: scoped by `BudgetId` only (remove `PeriodId`).
- Handler (Dapper): `SELECT ... FROM BudgetLines WHERE BudgetId = @budgetId AND DeletedAt IS NULL`; response DTO includes `startDate`, `endDate`, `budgetedAmount` (from latest revision where `ValidFrom <= today`); exclude `isRecurring`, `revisedAt`.
- Endpoint: `GET /api/budgets/{budgetId}/lines`.

**Tests to write** (`tests/MyBudget.Features.Tests/ListBudgetLinesHandlerTests.cs`):
- `Handle_ReturnsBudgetScopedLines` (REQ-READ-04).
- `Handle_SoftDeletedLinesExcluded`.
- `Handle_ResponseContainsStartDateEndDateBudgetedAmount`.

**Spec refs**: REQ-READ-04

---

### PR2-T6 — `ReorderBudgetLines` slice

**Files** (4 files):
- `Features/BudgetStructure/ReorderBudgetLines/ReorderBudgetLinesCommand.cs`
- `Features/BudgetStructure/ReorderBudgetLines/ReorderBudgetLinesValidator.cs`
- `Features/BudgetStructure/ReorderBudgetLines/ReorderBudgetLinesHandler.cs`
- `Features/BudgetStructure/ReorderBudgetLines/ReorderBudgetLinesEndpoint.cs`

**What to implement**:
- Scope reorder by `(BudgetId, CategoryGroupId, CategoryId)` — remove `PeriodId`.
- Endpoint: `PUT /api/budgets/{budgetId}/lines/order`.

**Tests to write** (`tests/MyBudget.Features.Tests/ReorderBudgetLinesHandlerTests.cs`):
- `Handle_ReassignsDisplayOrder_AtBudgetScope` (REQ-BL-05).

**Spec refs**: REQ-BL-05

---

### PR2-T7 — Remove BudgetLine cascade from `DeletePeriod`, `DeleteCycle`, `RestoreCycle` handlers

**Files**:
- `Features/BudgetStructure/DeletePeriod/DeletePeriodHandler.cs`
- `Features/BudgetStructure/DeleteCycle/DeleteCycleHandler.cs`
- `Features/BudgetStructure/RestoreCycle/RestoreCycleHandler.cs`

**What to implement**:
- `DeletePeriodHandler`: remove any soft-delete cascade to BudgetLines.
- `DeleteCycleHandler`: remove cascade soft-delete of BudgetLines via period IDs.
- `RestoreCycleHandler`: remove cascade restore of BudgetLines via period IDs.

**Tests to write** (`tests/MyBudget.Features.Tests/CascadeHandlerTests.cs`):
- `DeletePeriod_DoesNotSoftDeleteBudgetLines` (REQ-CYC-03).
- `DeleteCycle_DoesNotSoftDeleteBudgetLines` (REQ-CYC-03).
- `RestoreCycle_DoesNotRestoreBudgetLines` (REQ-RST-02).

**Spec refs**: REQ-CYC-03, REQ-RST-02

---

### PR2-T8 — `CreateExecutionRecord` handler — date-range intersection guard

**Files**:
- `Features/BudgetExecution/CreateExecutionRecord/CreateExecutionRecordHandler.cs`
- `Features/BudgetExecution/CreateExecutionRecord/CreateExecutionRecordValidator.cs` (if OperationDate range check belongs in validator)

**What to implement**:
- Replace `line.PeriodId == cmd.PeriodId` check with:
  `Period.StartDate >= BudgetLine.StartDate AND (BudgetLine.EndDate IS NULL OR Period.StartDate <= BudgetLine.EndDate)`.
  Mismatch → `Result.Fail("BUDGET_LINE_NOT_IN_PERIOD")` HTTP 422.
- `OperationDate` range check: falls within `MAX(Period.StartDate, BudgetLine.StartDate) .. MIN(Period.EndDate, BudgetLine.EndDate ?? Period.EndDate)`.
  Out of range → `Result.Fail("OPERATION_DATE_OUT_OF_RANGE")` HTTP 422.

**Tests to write** (`tests/MyBudget.Features.Tests/CreateExecutionRecordHandlerTests.cs`):
- `Handle_BudgetLineCoversperiod_Succeeds` (REQ-EXEC-7 happy path).
- `Handle_BudgetLineDoesNotCoverPeriod_ReturnsBudgetLineNotInPeriod` (REQ-EXEC-7 rejection).
- `Handle_PerpetuaBudgetLine_CoversAnyPeriod` (REQ-EXEC-7 perpetual).
- `Handle_OperationDateWithinIntersection_Succeeds` (REQ-EXEC-DATE-RANGE-1).
- `Handle_OperationDateBeforeBudgetLineStart_ReturnsOutOfRange` (REQ-EXEC-DATE-RANGE-1).
- `Handle_OperationDateAfterBudgetLineEnd_ReturnsOutOfRange` (REQ-EXEC-DATE-RANGE-1).
- `Handle_NullOperationDate_NoRangeError` (REQ-EXEC-DATE-RANGE-1 null case).

**Spec refs**: REQ-EXEC-7, REQ-EXEC-DATE-RANGE-1

---

### PR2-T9 — `ListPeriodExecutionTotals` handler — date-range JOIN + revision resolution

**Files**:
- `Features/BudgetExecution/ListPeriodExecutionTotals/ListPeriodExecutionTotalsHandler.cs`

**What to implement**:
- Replace `WHERE bl."PeriodId" = @PeriodId` with date-range intersection JOIN:
  `bl."BudgetId" = @BudgetId AND bl."StartDate" <= @PeriodStartDate AND (bl."EndDate" IS NULL OR bl."EndDate" >= @PeriodStartDate)`.
- Add revision amount resolution subquery/JOIN:
  `WHERE blr."ValidFrom" <= @PeriodStartDate AND (blr."ValidTo" IS NULL OR blr."ValidTo" >= @PeriodStartDate)`.

**Tests to write** (`tests/MyBudget.Features.Tests/ListPeriodExecutionTotalsHandlerTests.cs`):
- `Handle_BudgetLineCoveringPeriod_IncludedInTotals` (REQ-EXEC-TOTALS-1).
- `Handle_BudgetLineNotCoveringPeriod_ExcludedFromTotals` (REQ-EXEC-TOTALS-1).
- `Handle_CorrectRevisionAmountSelected_BasedOnPeriodStart` (REQ-EXEC-TOTALS-1 revision resolution).

**Spec refs**: REQ-EXEC-TOTALS-1

---

### PR2-T10 — `ListExecutionRecords` handler — remove Period JOIN, use `BudgetId` directly

**Files**:
- `Features/BudgetExecution/ListExecutionRecords/ListExecutionRecordsHandler.cs`

**What to implement**:
- Remove Period JOIN used to verify BudgetLine; join directly on `bl."BudgetId"`.
- Verify no remaining references to `bl."PeriodId"`.

**Tests to write** (`tests/MyBudget.Features.Tests/ListExecutionRecordsHandlerTests.cs`):
- `Handle_ReturnsRecordsForBudget` — smoke test confirming query executes without PeriodId join.

**Spec refs**: Design file-changes table (ListExecutionRecords)

---

**PR2 Commit sequence**:
1. `feat(slices): CreateBudgetLine — budget-scoped, date range, initial revision`
2. `feat(slices): UpdateBudgetLine — metadata + SplitRevision, IsClosed guard`
3. `feat(slices): DeleteBudgetLine — remove periodId from route and command`
4. `feat(slices): RestoreBudgetLine — remove periodId from route and command`
5. `feat(slices): ListBudgetLines — budget-scoped, date range response`
6. `feat(slices): ReorderBudgetLines — budget-scoped display order`
7. `feat(slices): remove BudgetLine cascade from DeletePeriod, DeleteCycle, RestoreCycle`
8. `feat(slices): CreateExecutionRecord — date-range intersection + OperationDate check`
9. `feat(slices): ListPeriodExecutionTotals — date-range JOIN + revision resolution`
10. `feat(slices): ListExecutionRecords — drop Period JOIN, join on BudgetId`

---

## PR3 — Frontend — COMPLETE (branch: feat/budget-line-redesign-pr3)

**Branch**: `feat/budget-line-redesign-pr3`
**Target**: `feat/budget-line-redesign-pr2` (PR2 branch)
**Est. lines**: ~300
**Dependencies**: PR2 merged into PR2 branch
**Note**: can start in parallel with PR4 once PR2 is merged.

### PR3-T1 — Update TypeScript types

**Files**:
- `frontend/src/features/budget-structure/types.ts`

**What to implement**:
- Remove `isRecurring`, `revisedAt` from `BudgetLineDto`.
- Add `startDate: string`, `endDate: string | null`, `budgetedAmount: number`.
- Add `CreateBudgetLineRequest` type: `startDate`, `endDate?`, `initialAmount`, `currencyId`.
- Add `UpdateBudgetLineRequest` type: `name?`, `categoryGroupId?`, `categoryId?`, `validFrom?`, `validTo?`, `newAmount?`, `currencyId?`.

**Tests to write** (`frontend/src/features/budget-structure/__tests__/types.test.ts`):
- Type-level only — validated implicitly by TypeScript compilation.
- Add Zod schema `BudgetLineDtoSchema` and test: `parse_ValidDto_Succeeds`, `parse_WithIsRecurring_Fails` (schema rejects old shape).

**Spec refs**: REQ-BL-ENTITY-1, REQ-BL-STORE-1

---

### PR3-T2 — Update `budgetLines.api.ts`

**Files**:
- `frontend/src/features/budget-structure/api/budgetLines.api.ts`

**What to implement**:
- Change base URL to `/api/budgets/${budgetId}/lines`.
- Remove `periodId` from all function signatures.
- Update `createLine`, `updateLine`, `deleteLine`, `restoreLine`, `reorderLines` to use new route shape.

**Tests to write** (`frontend/src/features/budget-structure/__tests__/budgetLines.api.test.ts`):
- `createLine_CallsCorrectUrl_WithoutPeriodId`.
- `updateLine_CallsCorrectUrl_WithValidFrom`.
- `deleteLine_CallsCorrectUrl_WithoutPeriodId`.
- `restoreLine_CallsCorrectUrl_WithoutPeriodId`.

**Spec refs**: REQ-BL-2, REQ-BL-4, REQ-RESTORE-1

---

### PR3-T3 — Update `budgetStructure` Pinia store

**Files**:
- `frontend/src/features/budget-structure/store.ts`

**What to implement**:
- Remove `periodId` from `loadLines`, `createLine`, `updateLine`, `deleteLine`, `restoreLine` action signatures.
- State keyed by `budgetId` only.
- `loadLines(budgetId)` fetches all lines for the budget (no period filter).

**Tests to write** (`frontend/src/features/budget-structure/__tests__/store.test.ts`):
- `loadLines_CallsApiWithBudgetIdOnly`.
- `createLine_UpdatesStateAfterSuccess`.
- `deleteLine_RemovesFromState`.

**Spec refs**: REQ-BL-STORE-1

---

### PR3-T4 — Update `BudgetLinesView.vue`

**Files**:
- `frontend/src/features/budget-structure/views/BudgetLinesView.vue`

**What to implement**:
- Remove `periodId` from route params and store calls.
- Add `startDate` and `endDate` columns to line table; remove `isRecurring` column.
- Pass `budgetId` from route params to store actions.

**Tests to write** (`frontend/src/features/budget-structure/__tests__/BudgetLinesView.test.ts`):
- `mounts_CallsLoadLinesWithBudgetId`.
- `renders_StartDateAndEndDate_Columns`.
- `doesNotRender_IsRecurring_Column` (REQ-BL-1).

**Spec refs**: REQ-BL-1

---

### PR3-T5 — Update `BudgetLineModal.vue`

**Files**:
- `frontend/src/features/budget-structure/components/BudgetLineModal.vue`

**What to implement**:
- Remove `isRecurring` input field.
- Add `startDate` (required, date input), `endDate` (optional, date input).
- For amount revision section: add `validFrom` (required when amount changes, min=today), `validTo` (optional).
- Client-side validation: `startDate` required; `endDate > startDate` when set; `validFrom >= today` when set.
- Add i18n keys: `budgetStructure.budgetLines.validation.startDateRequired`, `endDateAfterStartDate`, `validFromRequired`, `validFromNotInPast`, `validFromOutOfRange`.

**Tests to write** (`frontend/src/features/budget-structure/__tests__/BudgetLineModal.test.ts`):
- `renders_DateRangeInputs_NoIsRecurring` (REQ-BL-3).
- `validFrom_Required_WhenAmountChanges` (REQ-BL-3).
- `validFrom_BeforeToday_ShowsValidationError` (REQ-BL-3).
- `startDate_Required_BlocksSubmit` (REQ-BL-2).

**Spec refs**: REQ-BL-2, REQ-BL-3, REQ-I18N-1

---

### PR3-T6 — Update `BudgetLineRow.vue`

**Files**:
- `frontend/src/features/budget-structure/components/BudgetLineRow.vue`

**What to implement**:
- Remove `isRecurring` column display.
- Add `startDate` and `endDate` display cells.
- Delete button calls new route (no `periodId`).

**Tests to write** (`frontend/src/features/budget-structure/__tests__/BudgetLineRow.test.ts`):
- `renders_StartDateAndEndDate`.
- `doesNotRender_IsRecurring`.
- `delete_Calls_CorrectApi`.

**Spec refs**: REQ-BL-1

---

### PR3-T7 — Update `BudgetMatrixView.vue` — client-side active-for-period filter

**Files**:
- `frontend/src/features/budget-execution/views/BudgetMatrixView.vue`
- `frontend/src/features/budget-execution/store.ts`

**What to implement** (`BudgetMatrixView.vue`):
- Load all BudgetLines via `loadLines(budgetId)` (no periodId).
- Per period column: compute active lines using client-side filter:
  `BudgetLine.startDate <= period.startDate AND (endDate == null OR endDate >= period.startDate)`.
- Inactive cells: show 0, disable execution action buttons.

**What to implement** (`budget-execution/store.ts`):
- Remove `periodId` from `loadLines` call if present.

**Tests to write** (`frontend/src/features/budget-execution/__tests__/BudgetMatrixView.test.ts`):
- `activeLine_ShowsBudgetedAmount_WithActionsEnabled` (REQ-BL-MATRIX-1).
- `inactiveLine_ShowsZero_WithActionsDisabled` (REQ-BL-MATRIX-1).

**Spec refs**: REQ-BL-MATRIX-1

---

### PR3-T8 — Update i18n locale files

**Files**:
- `frontend/src/i18n/locales/en.json`
- `frontend/src/i18n/locales/es.json`

**What to implement**:
- Add keys under `budgetStructure.budgetLines.validation`:
  - `startDateRequired`
  - `endDateAfterStartDate`
  - `validFromRequired`
  - `validFromNotInPast`
  - `validFromOutOfRange`
- Remove `isRecurring` label key if present.

**Tests to write**: validated by PR3-T5 component tests (translation keys resolve).

**Spec refs**: REQ-I18N-1

---

### PR3-T9 — Period restore — remove BudgetLine cascade disclosure

**Files**:
- Component containing Period restore modal (e.g., `PeriodRestoreModal.vue` or equivalent).

**What to implement**:
- Remove any UI text about BudgetLines being restored alongside the Period.

**Tests to write**:
- `periodRestoreModal_DoesNotShowBudgetLineCascadeDisclosure` (REQ-RESTORE-PERIOD-1).

**Spec refs**: REQ-RESTORE-PERIOD-1

---

**PR3 Commit sequence**:
1. `feat(frontend): update BudgetLine TypeScript types and Zod schema`
2. `feat(frontend): update budgetLines.api — budget-scoped routes`
3. `feat(frontend): update budgetStructure store — remove periodId`
4. `feat(frontend): BudgetLinesView — date columns, no periodId`
5. `feat(frontend): BudgetLineModal — date range inputs, validFrom for revisions`
6. `feat(frontend): BudgetLineRow — date display, remove isRecurring`
7. `feat(frontend): BudgetMatrixView — client-side active-for-period filter`
8. `feat(frontend): i18n — add date-range validation keys`
9. `feat(frontend): period restore — remove BudgetLine cascade disclosure`

---

## PR4 — Integration Tests

**Branch**: `feat/budget-line-redesign-pr4`
**Target**: `feat/budget-line-redesign-pr2` (PR2 branch)
**Est. lines**: ~350
**Dependencies**: PR2 merged into PR2 branch
**Note**: parallel with PR3.

### PR4-T1 — BudgetStructure integration tests

**Files** (`tests/MyBudget.Integration.Tests/BudgetStructure/`):
- `CreateBudgetLineIntegrationTests.cs`
- `UpdateBudgetLineIntegrationTests.cs`
- `DeleteBudgetLineIntegrationTests.cs`
- `RestoreBudgetLineIntegrationTests.cs`
- `ListBudgetLinesIntegrationTests.cs`
- `ReorderBudgetLinesIntegrationTests.cs`

**Tests to write** (full HTTP integration — `WebApplicationFactory`, SQLite in-memory):

`CreateBudgetLine`:
- `POST_ValidPayload_Returns201_WithRevision` (REQ-BL-02 `@integration`).
- `POST_DuplicateName_SameBudget_Returns422_BUDGET_LINE_NAME_DUPLICATE` (REQ-BL-NAME-1 `@integration`).
- `POST_SameNameDifferentBudget_Returns201` (REQ-BL-NAME-1 scope `@integration`).
- `POST_SelfRename_Allowed_Returns200` (REQ-BL-NAME-1 self-exclusion `@integration`).

`UpdateBudgetLine`:
- `PUT_MetadataOnly_Returns200_RevisionCountUnchanged` (REQ-BL-03 `@integration`).
- `PUT_AmountRevision_Returns200_SplitsRevision` (REQ-BL-03 `@integration`).
- `PUT_ValidFromInClosedPeriod_Returns409_PERIOD_CLOSED` (REQ-BL-01 `@integration`).
- `PUT_MetadataOnly_AllPeriodsClosed_Returns200` (REQ-BL-01 metadata-only `@integration`).

`DeleteBudgetLine`:
- `DELETE_ActiveLine_Returns204_SoftDeletes` (REQ-BL-04 `@integration`).

`RestoreBudgetLine`:
- `POST_Restore_SoftDeleted_Returns200_ClearsDeletedAt` (REQ-RST-05 `@integration`).

`ListBudgetLines`:
- `GET_Returns200_BudgetScopedLines_WithDateRange` (REQ-READ-04 `@integration`).

`ReorderBudgetLines`:
- `PUT_ReorderAtBudgetScope_Returns200` (REQ-BL-05 `@integration`).

---

### PR4-T2 — BudgetExecution integration tests

**Files** (`tests/MyBudget.Integration.Tests/BudgetExecution/`):
- `CreateExecutionRecordIntegrationTests.cs`
- `ListPeriodExecutionTotalsIntegrationTests.cs`

**Tests to write**:

`CreateExecutionRecord`:
- `POST_BudgetLineCoversperiod_Returns201` (REQ-EXEC-7 `@integration`).
- `POST_BudgetLineNotCoverPeriod_Returns422_BUDGET_LINE_NOT_IN_PERIOD` (REQ-EXEC-7 `@integration`).
- `POST_PerpetuaLine_CoversAnyPeriod_Returns201` (REQ-EXEC-7 `@integration`).
- `POST_OperationDateWithinIntersection_Returns201` (REQ-EXEC-DATE-RANGE-1 `@integration`).
- `POST_OperationDateBeforeBudgetLineStart_Returns422_OPERATION_DATE_OUT_OF_RANGE` (REQ-EXEC-DATE-RANGE-1 `@integration`).
- `POST_OperationDateAfterBudgetLineEnd_Returns422_OPERATION_DATE_OUT_OF_RANGE` (REQ-EXEC-DATE-RANGE-1 `@integration`).

`ListPeriodExecutionTotals`:
- `GET_BudgetLineCoveringPeriod_IncludedInTotals` (REQ-EXEC-TOTALS-1 `@integration`).
- `GET_BudgetLineNotCoveringPeriod_ExcludedFromTotals` (REQ-EXEC-TOTALS-1 `@integration`).
- `GET_CorrectRevisionAmountSelected_BasedOnPeriodStart` (REQ-EXEC-TOTALS-1 `@integration`).

---

### PR4-T3 — Cascade handler integration tests

**Files** (`tests/MyBudget.Integration.Tests/BudgetStructure/`):
- `CascadeIntegrationTests.cs`

**Tests to write**:
- `DELETE_Cycle_DoesNotSoftDelete_BudgetLines` (REQ-CYC-03 `@integration`).
- `DELETE_Period_DoesNotSoftDelete_BudgetLines` (REQ-CYC-03 `@integration`).
- `POST_RestoreCycle_DoesNotRestore_BudgetLines` (REQ-RST-02 `@integration`).

---

### PR4-T4 — Rewrite stale existing unit/integration tests

**Files**: scan `tests/` for any test referencing `PeriodId`, `IsRecurring`, `RevisedAt`, or old routes (`/periods/{id}/lines`).

**What to implement**:
- Update test setup helpers: remove `PeriodId` from `BudgetLine` fixture builders; add `StartDate`/`EndDate`.
- Update assertion helpers: remove `revisedAt`/`isRecurring` from response assertions.
- Fix route strings in integration test HTTP clients.

**Tests**: these are the tests — no additional tests-for-tests needed.

---

**PR4 Commit sequence**:
1. `test(integration): BudgetStructure — CreateBudgetLine integration suite`
2. `test(integration): BudgetStructure — UpdateBudgetLine integration suite`
3. `test(integration): BudgetStructure — Delete/Restore/List/Reorder integration suites`
4. `test(integration): BudgetExecution — CreateExecutionRecord integration suite`
5. `test(integration): BudgetExecution — ListPeriodExecutionTotals integration suite`
6. `test(integration): cascade handlers — no BudgetLine cascade`
7. `test(chore): rewrite stale tests — remove PeriodId/IsRecurring/RevisedAt fixtures`

---

## Edge Cases Summary

| ID | Where | Description |
|----|-------|-------------|
| Edge Case A | PR2-T2 — `UpdateBudgetLineHandler` | IsClosed guard without PeriodId: query Periods by date-range intersection with `ValidFrom`; reject if any closed period found. |
| Edge Case B | PR1-T2 — `BudgetLine.SplitRevision` | If `newValidFrom == enclosing.ValidFrom`, overwrite enclosing revision in-place (replace amount/currencyId) instead of splitting — trimming would produce invalid `ValidTo = ValidFrom.AddDays(-1)`. |

## Parallelism Notes

| Tasks | Can parallelize? |
|-------|----------------|
| PR1-T1 and PR1-T3 | Yes (independent entities) |
| PR1-T2 and PR1-T4 | Sequential: T4 depends on T2 (entity shape) |
| PR3 and PR4 | Yes — both target PR2 branch independently |
| PR2-T8 and PR2-T9 | Yes — independent handlers in BudgetExecution |

## Task Count Summary

| PR | Tasks | Unit Tests | Integration Tests |
|----|-------|-----------|-------------------|
| PR1 | 5 | ~8 | 0 |
| PR2 | 10 | ~28 | 0 |
| PR3 | 9 | ~20 | 0 |
| PR4 | 4 | 0 | ~22 |
| **Total** | **28** | **~56** | **~22** |
