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

[Full task details from original tasks.md — see lines 150-385 for complete PR2-T1 through PR2-T10 specifications]

---

## PR3 — Frontend — COMPLETE (branch: feat/budget-line-redesign-pr3)

**Branch**: `feat/budget-line-redesign-pr3`
**Target**: `feat/budget-line-redesign-pr2` (PR2 branch)
**Est. lines**: ~300
**Dependencies**: PR2 merged into PR2 branch
**Note**: can start in parallel with PR4 once PR2 is merged.

[Full task details from original tasks.md — see lines 388-583 for complete PR3-T1 through PR3-T9 specifications]

---

## PR4 — Integration Tests (branch: feat/budget-line-redesign-pr4)

**Branch**: `feat/budget-line-redesign-pr4`
**Target**: `feat/budget-line-redesign-pr2` (PR2 branch)
**Est. lines**: ~350
**Dependencies**: PR2 merged into PR2 branch
**Note**: parallel with PR3.

- [x] T1: BudgetStructure integration tests (Create/Update/Delete/Restore/List/Reorder)
- [x] T2: BudgetExecution integration tests (CreateExecutionRecord, ListPeriodExecutionTotals)
- [x] T3: Cascade handler integration tests (DeleteCycle, DeletePeriod, RestoreCycle)
- [x] T4: Rewrite stale tests (remove PeriodId/IsRecurring/RevisedAt fixtures)

[Full task details from original tasks.md — see lines 586-693 for complete PR4-T1 through PR4-T4 specifications]

---

## Task Count Summary

| PR | Tasks | Unit Tests | Integration Tests |
|----|-------|-----------|-------------------|
| PR1 | 5 | ~8 | 0 |
| PR2 | 10 | ~28 | 0 |
| PR3 | 9 | ~20 | 0 |
| PR4 | 4 | 0 | ~22 |
| **Total** | **28** | **~56** | **~22** |
