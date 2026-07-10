# Tasks: Budget Structure

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~1480 (entities+configs+migration ~350, PR2 write slices ~380, PR3 write slices ~350, PR4 reads+tests ~400) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 (entities+migration) → PR2 (cycle/period/cg slices) → PR3 (reorder/cat/bl slices) → PR4 (reads+tests) |
| Delivery strategy | 4 chained PRs (feature-branch-chain) |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Entities + EF configs + migration | PR1 | Base: `feat/budget-structure`; no runtime behavior |
| 2 | Write slices: Cycle + Period + CategoryGroup | PR2 | Base: PR1 branch; 11 slices × 4 files |
| 3 | Write slices: Reorder + Category + BudgetLine | PR3 | Base: PR2 branch; 8 slices × 4 files |
| 4 | Read slices + all tests | PR4 | Base: PR3 branch; 4 read slices + ~113 tests |

---

## PR1 — Entities + Migration

- [x] PR1.1: Update dotnet-ef tooling — run `dotnet tool update --global dotnet-ef` to resolve version mismatch (tools 9.x vs runtime 10.x)
- [x] PR1.2: Add `LineType` enum (`Expense = 0`, `LongTermSavings = 1`, `PreventiveSavings = 2`) to `SharedKernel/Entities/LineType.cs`
- [x] PR1.3: Create `SharedKernel/Entities/Cycle.cs` — extends `BaseEntity`; fields: `BudgetId`, `Name`, `StartDate`, `EndDate`, `IsActive`, `DeletedAt`; nav props; domain methods `Activate()`, `Deactivate()`
- [x] PR1.4: Create `SharedKernel/Entities/Period.cs` — extends `BaseEntity`; fields: `CycleId`, `Name`, `PeriodNumber`, `StartDate`, `EndDate`, `IsClosed`, `DeletedAt`; nav props; domain method `SetClosed(bool)`
- [x] PR1.5: Create `SharedKernel/Entities/CategoryGroup.cs` — extends `BaseEntity`; fields: `BudgetId`, `Name`, `DisplayOrder`, `DeletedAt`; nav props; method `SetDisplayOrder(int)`
- [x] PR1.6: Create `SharedKernel/Entities/Category.cs` — extends `BaseEntity`; fields: `CategoryGroupId`, `Name`, `DisplayOrder`, `DeletedAt`; nav props; method `SetDisplayOrder(int)`
- [x] PR1.7: Create `SharedKernel/Entities/BudgetLine.cs` — extends `BaseEntity`; fields: `PeriodId`, `CategoryGroupId`, `CategoryId?`, `Name`, `LineType`, `IsRecurring`, `DeletedAt`; nav props
- [x] PR1.8: Create `SharedKernel/Entities/BudgetLineRevision.cs` — extends `BaseEntity`; fields: `BudgetLineId`, `BudgetedAmount`, `Currency` (max 3), `RevisedAt`, `Note?`; NO `DeletedAt`; nav props
- [x] PR1.9: Create `SharedKernel/Persistence/Configurations/CycleConfiguration.cs` — table "Cycles"; IX_Cycles_BudgetId; IX_Cycles_BudgetId_IsActive (partial unique, IsActive=true); query filter `DeletedAt == null`; restrict FK from Budget
- [x] PR1.10: Create `SharedKernel/Persistence/Configurations/PeriodConfiguration.cs` — table "Periods"; IX_Periods_CycleId; query filter `DeletedAt == null`; cascade FK from Cycle
- [x] PR1.11: Create `SharedKernel/Persistence/Configurations/CategoryGroupConfiguration.cs` — table "CategoryGroups"; IX_CategoryGroups_BudgetId; IX_CategoryGroups_BudgetId_Name (unique); query filter `DeletedAt == null`; restrict FK from Budget
- [x] PR1.12: Create `SharedKernel/Persistence/Configurations/CategoryConfiguration.cs` — table "Categories"; IX_Categories_CategoryGroupId; IX_Categories_CategoryGroupId_Name (unique); query filter `DeletedAt == null`; cascade FK from CategoryGroup
- [x] PR1.13: Create `SharedKernel/Persistence/Configurations/BudgetLineConfiguration.cs` — table "BudgetLines"; IX_BudgetLines_PeriodId; IX_BudgetLines_CategoryGroupId; query filter `DeletedAt == null`; cascade FK from Period
- [x] PR1.14: Create `SharedKernel/Persistence/Configurations/BudgetLineRevisionConfiguration.cs` — table "BudgetLineRevisions"; IX_BudgetLineRevisions_BudgetLineId_RevisedAt (desc); NO query filter; cascade FK from BudgetLine; Currency `HasMaxLength(3)`
- [x] PR1.15: Modify `SharedKernel/Persistence/AppDbContext.cs` — add 6 `DbSet<T>` properties; apply 6 configurations in `OnModelCreating`
- [x] PR1.16: Add EF migration `AddBudgetStructureTables` — run `dotnet ef migrations add AddBudgetStructureTables --project ...`; verify snapshot is correct; do NOT apply to DB yet

---

## PR2 — Write Slices: Cycles + Periods + CategoryGroup

Each slice = 4 files: `{SliceName}Command.cs`, `{SliceName}Validator.cs`, `{SliceName}Handler.cs`, `{SliceName}Endpoint.cs`

- [x] PR2.1: `@unit` Write failing validator tests for `CreateCycleValidator` (StartDate < EndDate; required Name; overlap scenario stub)
- [x] PR2.2: Implement `Features/BudgetStructure/CreateCycle/` — POST `/api/budgets/{id}/cycles`; date-overlap check; returns 201; satisfies REQ-CYC-01
- [x] PR2.3: `@unit` Write failing validator tests for `UpdateCycleValidator` (same rules + period-out-of-range stub)
- [x] PR2.4: Implement `Features/BudgetStructure/UpdateCycle/` — PUT `/api/budgets/{id}/cycles/{cycleId}`; excludes self from overlap check; validates no Period falls outside new range; returns 200; satisfies REQ-CYC-02
- [x] PR2.5: `@unit` Write failing validator tests for `DeleteCycleValidator` (cycleId required)
- [x] PR2.6: Implement `Features/BudgetStructure/DeleteCycle/` — DELETE `/api/budgets/{id}/cycles/{cycleId}`; sets `DeletedAt` on Cycle + child Periods + their BudgetLines in one `SaveChangesAsync`; returns 204; satisfies REQ-CYC-03
- [x] PR2.7: `@unit` Write failing validator tests for `SetActiveCycleValidator` (cycleId required, must belong to budget)
- [x] PR2.8: Implement `Features/BudgetStructure/SetActiveCycle/` — PUT `/api/budgets/{id}/active-cycle`; atomic swap: load current active, deactivate, activate target, single `SaveChangesAsync`; returns 200; satisfies REQ-CYC-04
- [x] PR2.9: `@unit` Write failing validator tests for `CreatePeriodValidator` (required fields; PeriodNumber > 0)
- [x] PR2.10: Implement `Features/BudgetStructure/CreatePeriod/` — POST `/api/budgets/{id}/cycles/{cycleId}/periods`; validates dates within Cycle range; checks overlap within Cycle; returns 201; satisfies REQ-PER-01
- [x] PR2.11: `@unit` Write failing validator tests for `UpdatePeriodValidator`
- [x] PR2.12: Implement `Features/BudgetStructure/UpdatePeriod/` — PUT `.../periods/{periodId}`; same range/overlap rules; returns 200; satisfies REQ-PER-02
- [x] PR2.13: `@unit` Write failing validator tests for `SetPeriodStatusValidator` (isClosed required)
- [x] PR2.14: Implement `Features/BudgetStructure/SetPeriodStatus/` — PATCH `.../periods/{periodId}/status`; sets `IsClosed`; returns 200; satisfies REQ-PER-03
- [x] PR2.15: `@unit` Write failing validator tests for `DeletePeriodValidator`
- [x] PR2.16: Implement `Features/BudgetStructure/DeletePeriod/` — DELETE `.../periods/{periodId}`; sets `DeletedAt` on Period + BudgetLines + Revisions; returns 204; satisfies REQ-PER-04
- [x] PR2.17: `@unit` Write failing validator tests for `CreateCategoryGroupValidator` (Name required, max 200; DisplayOrder > 0)
- [x] PR2.18: Implement `Features/BudgetStructure/CreateCategoryGroup/` — POST `/api/budgets/{id}/category-groups`; case-insensitive unique name per budget; returns 201; satisfies REQ-CG-01
- [x] PR2.19: `@unit` Write failing validator tests for `UpdateCategoryGroupValidator`
- [x] PR2.20: Implement `Features/BudgetStructure/UpdateCategoryGroup/` — PUT `.../category-groups/{groupId}`; unique name excluding self; returns 200; satisfies REQ-CG-02
- [x] PR2.21: `@unit` Write failing validator tests for `DeleteCategoryGroupValidator`
- [x] PR2.22: Implement `Features/BudgetStructure/DeleteCategoryGroup/` — DELETE `.../category-groups/{groupId}`; sets `DeletedAt` on group + all its Categories; returns 204; satisfies REQ-CG-03

---

## PR3 — Write Slices: Reorder + Categories + BudgetLines

- [ ] PR3.1: `@unit` Write failing validator tests for `ReorderCategoryGroupsValidator` (list non-empty; completeness check stub)
- [ ] PR3.2: `@unit` Write failing handler unit test for `ReorderCategoryGroupsHandler` — incomplete list → 422 `REORDER_LIST_INCOMPLETE`
- [ ] PR3.3: Implement `Features/BudgetStructure/ReorderCategoryGroups/` — PUT `.../category-groups/order`; load all non-deleted groups; validate list completeness and no duplicates; assign `DisplayOrder = index + 1`; returns 204; satisfies REQ-CG-04
- [ ] PR3.4: `@unit` Write failing validator tests for `CreateCategoryValidator` (Name required; DisplayOrder > 0)
- [ ] PR3.5: Implement `Features/BudgetStructure/CreateCategory/` — POST `.../category-groups/{groupId}/categories`; unique name within group (case-insensitive); returns 201; satisfies REQ-CAT-01
- [ ] PR3.6: `@unit` Write failing validator tests for `UpdateCategoryValidator`
- [ ] PR3.7: Implement `Features/BudgetStructure/UpdateCategory/` — PUT `.../categories/{categoryId}`; unique name within group excluding self; returns 200; satisfies REQ-CAT-02
- [ ] PR3.8: `@unit` Write failing validator tests for `DeleteCategoryValidator`
- [ ] PR3.9: Implement `Features/BudgetStructure/DeleteCategory/` — DELETE `.../categories/{categoryId}`; sets `DeletedAt` on category only (BudgetLines retain reference); returns 204; satisfies REQ-CAT-03
- [ ] PR3.10: `@unit` Write failing validator tests for `ReorderCategoriesValidator`
- [ ] PR3.11: `@unit` Write failing handler unit test for `ReorderCategoriesHandler` — incomplete list → 422 `REORDER_LIST_INCOMPLETE`
- [ ] PR3.12: Implement `Features/BudgetStructure/ReorderCategories/` — PUT `.../categories/order`; same pattern as ReorderCategoryGroups scoped to CategoryGroupId; returns 204; satisfies REQ-CAT-04
- [ ] PR3.13: `@unit` Write failing validator tests for `CreateBudgetLineValidator` (Name required; LineType must be Expense/LongTermSavings/PreventiveSavings; Currency must be GTQ or USD; Amount > 0)
- [ ] PR3.14: `@unit` Write failing handler unit test for `CreateBudgetLineHandler` — IsClosed period → 409 `PERIOD_CLOSED`; initial revision auto-created
- [ ] PR3.15: Implement `Features/BudgetStructure/CreateBudgetLine/` — POST `.../periods/{periodId}/lines`; load Period → verify Cycle.BudgetId; check `IsClosed`; create `BudgetLine` + initial `BudgetLineRevision`; returns 201; satisfies REQ-BL-01, REQ-BL-02
- [ ] PR3.16: `@unit` Write failing validator tests for `UpdateBudgetLineValidator`
- [ ] PR3.17: `@unit` Write failing handler unit test for `UpdateBudgetLineHandler` — IsClosed → 409; new revision created; existing revisions unchanged
- [ ] PR3.18: Implement `Features/BudgetStructure/UpdateBudgetLine/` — PUT `.../lines/{lineId}`; check IsClosed; update line fields; insert new `BudgetLineRevision`; returns 200; satisfies REQ-BL-01, REQ-BL-03
- [ ] PR3.19: `@unit` Write failing validator tests for `DeleteBudgetLineValidator`
- [ ] PR3.20: `@unit` Write failing handler unit test for `DeleteBudgetLineHandler` — IsClosed → 409; soft-delete cascades to Revisions
- [ ] PR3.21: Implement `Features/BudgetStructure/DeleteBudgetLine/` — DELETE `.../lines/{lineId}`; check IsClosed; sets `DeletedAt` on BudgetLine + all its BudgetLineRevisions; returns 204; satisfies REQ-BL-01, REQ-BL-04

---

## PR4 — Read Slices + Tests

Each read slice = 3 files: `{SliceName}Query.cs`, `{SliceName}Handler.cs`, `{SliceName}Endpoint.cs`

- [ ] PR4.1: Implement `Features/BudgetStructure/ListCycles/` — GET `/api/budgets/{id}/cycles`; Dapper query; returns all non-deleted Cycles ordered by StartDate with `isActive` flag and period count; satisfies REQ-READ-01
- [ ] PR4.2: Implement `Features/BudgetStructure/GetCycleDetail/` — GET `/api/budgets/{id}/cycles/{cycleId}`; Dapper query; returns Cycle + nested Periods ordered by PeriodNumber; 404 if not found; satisfies REQ-READ-02
- [ ] PR4.3: Implement `Features/BudgetStructure/ListCategoryGroups/` — GET `/api/budgets/{id}/category-groups`; Dapper query; returns non-deleted groups ordered by DisplayOrder with nested non-deleted Categories ordered by DisplayOrder; satisfies REQ-READ-03
- [ ] PR4.4: Implement `Features/BudgetStructure/ListBudgetLines/` — GET `/api/budgets/{id}/periods/{periodId}/lines`; Dapper query with LATERAL JOIN for latest BudgetLineRevision (see design.md SQL); ordered by CategoryGroup DisplayOrder, Category DisplayOrder, BudgetLine Name; satisfies REQ-READ-04
- [ ] PR4.5: `@unit` Validator unit tests — all remaining validators not covered in PR2/PR3 (verify ~38 total tests pass)
- [ ] PR4.6: `@unit` Handler unit tests — `SetActiveCycle` atomic swap (CycleA→false, CycleB→true in one transaction); no prior active cycle path; satisfies REQ-CYC-04
- [ ] PR4.7: `@unit` Handler unit tests — `ReorderCategoryGroups` incomplete list rejection; duplicate IDs rejection; correct DisplayOrder assignment
- [ ] PR4.8: `@unit` Handler unit tests — `ReorderCategories` incomplete list rejection; correct DisplayOrder assignment scoped to group
- [ ] PR4.9: `@unit` Handler unit tests — IsClosed guard on Create/Update/Delete BudgetLine (3 paths)
- [ ] PR4.10: `@unit` Handler unit test — revision auto-create on `CreateBudgetLine` and `UpdateBudgetLine`; existing Revision rows are byte-for-byte unchanged after update
- [ ] PR4.11: `@integration` Integration tests — Cycle endpoints: CreateCycle happy path, date overlap 422, StartDate>EndDate 422; UpdateCycle happy path, period-out-of-range 422; DeleteCycle cascade soft-delete; SetActiveCycle swap, no-prior-active; 401/403 auth checks; satisfies REQ-CYC-01 to REQ-CYC-04
- [ ] PR4.12: `@integration` Integration tests — Period endpoints: CreatePeriod happy path, dates-outside-range 422, date-overlap 422; UpdatePeriod happy path; SetPeriodStatus close/reopen; DeletePeriod cascade; 401/403; satisfies REQ-PER-01 to REQ-PER-04
- [ ] PR4.13: `@integration` Integration tests — CategoryGroup endpoints: Create happy/duplicate-name 422; Update happy; Delete cascade; ReorderCategoryGroups happy/incomplete-list 422; 401/403; satisfies REQ-CG-01 to REQ-CG-04
- [ ] PR4.14: `@integration` Integration tests — Category endpoints: Create happy/duplicate-name 422; Update happy; Delete soft-delete; ReorderCategories happy/incomplete-list 422; 401/403; satisfies REQ-CAT-01 to REQ-CAT-04
- [ ] PR4.15: `@integration` Integration tests — BudgetLine endpoints: Create happy (with/without category), IsClosed 409, invalid LineType 422, invalid currency 422; Update happy/IsClosed 409; Delete happy/IsClosed 409; satisfies REQ-BL-01 to REQ-BL-04
- [ ] PR4.16: `@integration` Integration tests — Read endpoints: ListCycles happy; GetCycleDetail happy/404; ListCategoryGroups happy; ListBudgetLines happy (latest revision shown), budget:read caller succeeds, budget:admin required for writes; satisfies REQ-READ-01 to REQ-READ-04
- [ ] PR4.17: `@integration` Integration tests — Resource isolation: cross-budget access returns 404 for Cycles, Periods, CategoryGroups, Categories, BudgetLines; satisfies REQ-SC-03
