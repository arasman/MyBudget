# Tasks: Budget Structure Patch

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 900–1100 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 (Currency + Cycle) → PR2 (BudgetLine currency + DisplayOrder) → PR3 (Restore endpoints) |
| Delivery strategy | auto-chain |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Currency entity + Cycle currency fields + Cycle endpoints + GET currencies | PR1 | Base: `main` → branch `feat/budget-patch-currency` |
| 2 | BudgetLineRevision CurrencyId + BudgetLine DisplayOrder + ReorderBudgetLines | PR2 | Base: PR1 branch → `feat/budget-patch-budgetline` |
| 3 | Restore() on all entities + 4 restore slices | PR3 | Base: PR2 branch → `feat/budget-patch-restore` |

---

## PR1 — Currency + Cycle (~350 lines)

### Phase 1.1: Foundation

- [x] PR1.1 Create `SharedKernel/Entities/CurrencySeeds.cs` — static class with `GtqId`, `UsdId`, `EurId` well-known `Guid` constants (CUR-2)
- [x] PR1.2 Create `SharedKernel/Entities/Currency.cs` — `Id`, `Code`, `Name`, `Symbol`; private ctor; no `BaseEntity` (CUR-1, CUR-3)
- [x] PR1.3 Create `Persistence/Configurations/CurrencyConfiguration.cs` — table `Currencies`, unique index on `Code`, `HasData()` with 3 seed rows using `CurrencySeeds` GUIDs (CUR-2)
- [x] PR1.4 Modify `SharedKernel/Persistence/AppDbContext.cs` — add `DbSet<Currency> Currencies` (CUR-1)
- [x] PR1.5 Modify `Persistence/Configurations/CycleConfiguration.cs` — add two FK relationships to `Currency`; add `HasPrecision(18, 6)` for `ExchangeRate` (CYC-1, CYC-2, CYC-3)
- [x] PR1.6 Modify `SharedKernel/Entities/Cycle.cs` — add `DefaultCurrencyId`, `AlternateCurrencyId`, `ExchangeRate` props + navigation; extend `Create()` and `Update()`; add `Restore()` (CYC-1–3, RST-1)
- [x] PR1.7 Create migration `Migrations/20260711183245_AddCurrencyAndCycleFields.cs` — steps 1–3 only: CREATE Currencies + seed rows + ALTER Cycles (CUR-1, CUR-2, CYC-1–3)

### Phase 1.2: Slices

- [x] PR1.8 Create `Features/BudgetStructure/ListCurrencies/ListCurrenciesQuery.cs` (CUR-4)
- [x] PR1.9 Create `Features/BudgetStructure/ListCurrencies/ListCurrenciesHandler.cs` — Dapper `SELECT * FROM "Currencies"` (CUR-4)
- [x] PR1.10 Create `Features/BudgetStructure/ListCurrencies/ListCurrenciesEndpoint.cs` — `GET /budgets/{budgetId}/currencies` (CUR-4)
- [x] PR1.11 Modify `Features/BudgetStructure/CreateCycle/CreateCycleCommand.cs` — add `DefaultCurrencyId`, `AlternateCurrencyId`, `ExchangeRate` (CYC-6)
- [x] PR1.12 Modify `Features/BudgetStructure/CreateCycle/CreateCycleValidator.cs` — add `CYC_PAIR_INCOMPLETE` rule (CYC-4, CYC-6)
- [x] PR1.13 Modify `Features/BudgetStructure/CreateCycle/CreateCycleHandler.cs` — pass currency fields to `Cycle.Create()` (CYC-6)
- [x] PR1.14 Modify `Features/BudgetStructure/UpdateCycle/UpdateCycleCommand.cs` — add currency fields (CYC-7)
- [x] PR1.15 Modify `Features/BudgetStructure/UpdateCycle/UpdateCycleValidator.cs` — add `CYC_PAIR_INCOMPLETE` rule (CYC-4, CYC-7)
- [x] PR1.16 Modify `Features/BudgetStructure/UpdateCycle/UpdateCycleHandler.cs` — pass currency fields to `Cycle.Update()` (CYC-7)
- [x] PR1.17 Modify `Features/BudgetStructure/GetCycleDetail/GetCycleDetailQuery.cs` + Handler — JOIN `Currencies`; extend response with `defaultCurrency`, `alternateCurrency`, `exchangeRate` (CYC-8)
- [x] PR1.18 Modify `Features/BudgetStructure/ListCycles/ListCyclesQuery.cs` + Handler — JOIN `Currencies`; extend response with `defaultCurrency` (CYC-9)

### Phase 1.3: Tests

- [x] PR1.19 Test `Currency` entity — instantiation via seeded data matches `CurrencySeeds` GUIDs (CUR-1, CUR-2)
- [x] PR1.20 Test `Cycle` entity — `Create()` sets currency fields; `Update()` updates them; `Restore()` sets `DeletedAt=null` and refreshes `UpdatedAt` (CYC-1–3, RST-1)
- [x] PR1.21 Test `CreateCycleValidator` — `CYC_PAIR_INCOMPLETE` on AlternateCurrencyId-only and ExchangeRate-only inputs (CYC-4)
- [x] PR1.22 Test `UpdateCycleValidator` — same pair rule on update path (CYC-4, CYC-7)
- [x] PR1.23 Test `CreateCycleHandler` — currency fields persisted correctly (CYC-6)
- [x] PR1.24 Test `UpdateCycleHandler` — currency fields updated correctly (CYC-7)

---

## PR2 — BudgetLine Currency + DisplayOrder (~350 lines)
*Base branch: `feat/budget-patch-currency` (requires PR1)*

### Phase 2.1: Foundation

- [x] PR2.1 Modify `SharedKernel/Entities/BudgetLineRevision.cs` — replace `Currency string` with `CurrencyId Guid` + navigation; update `Create()` signature (BLR-1)
- [x] PR2.2 Modify `Persistence/Configurations/BudgetLineRevisionConfiguration.cs` — replace varchar `Currency` config with `CurrencyId` FK to `Currencies` (BLR-1)
- [x] PR2.3 Modify `SharedKernel/Entities/BudgetLine.cs` — add `DisplayOrder int`, `SetDisplayOrder()`, `Restore()`; extend `Create()` with `displayOrder` param (BLD-1, RST-1)
- [x] PR2.4 Modify `Persistence/Configurations/BudgetLineConfiguration.cs` — add `DisplayOrder` property mapping (BLD-1)
- [x] PR2.5 Extend migration (or new migration) — steps 4–7: DELETE BudgetLineRevisions + ALTER BudgetLineRevisions + ALTER BudgetLines + backfill `DisplayOrder` via `ROW_NUMBER()` (BLR-2, BLD-2)

### Phase 2.2: Slices

- [x] PR2.6 Modify `Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineCommand.cs` — replace `Currency string` with `CurrencyId Guid?` (BLR-3)
- [x] PR2.7 Modify `Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineHandler.cs` — resolve `Cycle.DefaultCurrencyId` via `Period.Include(Cycle)` when `CurrencyId` absent (BLR-3)
- [x] PR2.8 Modify `Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineCommand.cs` — replace `Currency string` with `CurrencyId Guid?` (BLR-4)
- [x] PR2.9 Modify `Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineHandler.cs` — resolve `Cycle.DefaultCurrencyId` when absent (BLR-4)
- [x] PR2.10 Modify `Features/BudgetStructure/ListBudgetLines/ListBudgetLinesQuery.cs` + Handler — JOIN `Currencies` via revision; replace `Currency string?` with `currency: { code, symbol }` in response (BLR-5)
- [x] PR2.11 Create `Features/BudgetStructure/ReorderBudgetLines/ReorderBudgetLinesCommand.cs` — ordered `Guid[]` + `PeriodId` (BLD-3)
- [x] PR2.12 Create `Features/BudgetStructure/ReorderBudgetLines/ReorderBudgetLinesValidator.cs` — `REORDER_ID_NOT_IN_SCOPE`, `REORDER_DUPLICATE_ID` (BLD-3)
- [x] PR2.13 Create `Features/BudgetStructure/ReorderBudgetLines/ReorderBudgetLinesHandler.cs` — mirror `ReorderCategories`; scope IDs by `PeriodId` (BLD-3)
- [x] PR2.14 Create `Features/BudgetStructure/ReorderBudgetLines/ReorderBudgetLinesEndpoint.cs` — `PUT /budgets/{budgetId}/periods/{periodId}/budget-lines/order` (BLD-3)

### Phase 2.3: Tests

- [x] PR2.15 Test `BudgetLineRevision` entity — `Create()` accepts `CurrencyId Guid`; no `Currency` string field (BLR-1)
- [x] PR2.16 Test `BudgetLine` entity — `Create()` accepts `displayOrder`; `Restore()` clears `DeletedAt` (BLD-1, RST-1)
- [x] PR2.17 Test `CreateBudgetLineHandler` — explicit `CurrencyId` used; absent `CurrencyId` resolves to `Cycle.DefaultCurrencyId` (BLR-3)
- [x] PR2.18 Test `ReorderBudgetLinesHandler` — valid reorder, `REORDER_ID_NOT_IN_SCOPE`, `REORDER_DUPLICATE_ID` (BLD-3)

---

## PR3 — Restore Endpoints (~300 lines)
*Base branch: `feat/budget-patch-budgetline` (requires PR2)*

### Phase 3.1: Entity Restore Methods

- [x] PR3.1 Modify `SharedKernel/Entities/Period.cs` — add `Restore()` method (RST-1)
- [x] PR3.2 Modify `SharedKernel/Entities/CategoryGroup.cs` — add `Restore()` method (RST-1)
- [x] PR3.3 Modify `SharedKernel/Entities/Category.cs` — add `Restore()` method (RST-1)

### Phase 3.2: Restore Slices

- [x] PR3.4 Create `Features/BudgetStructure/RestoreCycle/RestoreCycleCommand.cs` — `(BudgetId, CycleId, IncludeExecutionRecords)` (RST-2, RST-6)
- [x] PR3.5 Create `Features/BudgetStructure/RestoreCycle/RestoreCycleValidator.cs` (RST-2)
- [x] PR3.6 Create `Features/BudgetStructure/RestoreCycle/RestoreCycleHandler.cs` — `IgnoreQueryFilters`; cascade Cycle→Periods→BudgetLines; already-active → 404 (RST-2, RST-7)
- [x] PR3.7 Create `Features/BudgetStructure/RestoreCycle/RestoreCycleEndpoint.cs` — `POST /budgets/{budgetId}/cycles/{cycleId}/restore` (RST-2, RST-6)
- [x] PR3.8 Create `Features/BudgetStructure/RestoreCategoryGroup/RestoreCategoryGroupCommand.cs` (RST-3)
- [x] PR3.9 Create `Features/BudgetStructure/RestoreCategoryGroup/RestoreCategoryGroupValidator.cs` (RST-3)
- [x] PR3.10 Create `Features/BudgetStructure/RestoreCategoryGroup/RestoreCategoryGroupHandler.cs` — cascade Group→Categories→BudgetLines by `CategoryGroupId`; no parent guard (Budget not soft-deletable) (RST-3, RST-7)
- [x] PR3.11 Create `Features/BudgetStructure/RestoreCategoryGroup/RestoreCategoryGroupEndpoint.cs` — `POST /budgets/{budgetId}/category-groups/{groupId}/restore` (RST-3)
- [x] PR3.12 Create `Features/BudgetStructure/RestoreCategory/RestoreCategoryCommand.cs` (RST-4)
- [x] PR3.13 Create `Features/BudgetStructure/RestoreCategory/RestoreCategoryValidator.cs` (RST-4)
- [x] PR3.14 Create `Features/BudgetStructure/RestoreCategory/RestoreCategoryHandler.cs` — cascade Category→BudgetLines by `CategoryId`; parent guard: `CategoryGroup.DeletedAt != null` → `409 PARENT_IS_DELETED` (RST-4, RST-7)
- [x] PR3.15 Create `Features/BudgetStructure/RestoreCategory/RestoreCategoryEndpoint.cs` — `POST /budgets/{budgetId}/categories/{categoryId}/restore` (RST-4)
- [x] PR3.16 Create `Features/BudgetStructure/RestoreBudgetLine/RestoreBudgetLineCommand.cs` (RST-5)
- [x] PR3.17 Create `Features/BudgetStructure/RestoreBudgetLine/RestoreBudgetLineValidator.cs` (RST-5)
- [x] PR3.18 Create `Features/BudgetStructure/RestoreBudgetLine/RestoreBudgetLineHandler.cs` — single restore; parent guard: `Period.DeletedAt != null` → `409 PARENT_IS_DELETED` (RST-5, RST-7)
- [x] PR3.19 Create `Features/BudgetStructure/RestoreBudgetLine/RestoreBudgetLineEndpoint.cs` — `POST /budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/restore` (RST-5)

### Phase 3.3: Tests

- [x] PR3.20 Test `Period`, `CategoryGroup`, `Category` `Restore()` — `DeletedAt=null`, `UpdatedAt` refreshed (RST-1)
- [x] PR3.21 Test `RestoreCycleHandler` — full cascade (Cycle+Periods+BudgetLines); already-active → 404; non-restored Period's BudgetLines not touched (RST-2)
- [x] PR3.22 Test `RestoreCategoryGroupHandler` — cascade Group→Categories→BudgetLines by `CategoryGroupId` (RST-3)
- [x] PR3.23 Test `RestoreCategoryHandler` — cascade + parent-deleted guard returns `409 PARENT_IS_DELETED` when `CategoryGroup.DeletedAt` is set (RST-4, RST-7)
- [x] PR3.24 Test `RestoreBudgetLineHandler` — single restore + parent-deleted guard returns `409 PARENT_IS_DELETED` when `Period.DeletedAt` is set (RST-5, RST-7)
