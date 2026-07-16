# Design: budget-execution-ui-patch

## Technical Approach

Two-phase patch: Phase 1 fixes frontend bugs and wires DnD/footer/inline-category with a single backend read-model extension (add `CurrencyId` to `BudgetLineResponse`). Phase 2 adds `OperationDate` to `ExecutionRecord` entity + migration and exposes it alongside `CurrencyId`/`ExchangeRate` in `ExecutionRecordForm.vue`. All changes follow existing VSA patterns; no new slices are created.

## Architecture Decisions

### Decision: Currency fix -- rename frontend field vs. add backend alias

| Option | Tradeoff | Chosen |
|--------|----------|--------|
| Rename FE `currency` to `currencyId`, send Guid | Breaking for callers of `CreateBudgetLinePayload`; one-time fix | **Yes** |
| Add backend `Currency` string alias alongside `CurrencyId` | Hides the real bug; two fields for one concept | No |

**Rationale**: The root cause is a type mismatch (`string` code vs `Guid`). Renaming the frontend field to `currencyId` and populating it from a new `CurrencyId` property on `BudgetLineResponse` is the clean fix. All callers of `CreateBudgetLinePayload`/`UpdateBudgetLinePayload` are in-tree and auditable.

### Decision: DnD integration pattern

| Option | Tradeoff | Chosen |
|--------|----------|--------|
| `vue-draggable-plus` wrapping each section's rows | Proven library, already installed; works with `<tbody>` by using `tag="tbody"` or template wrapping | **Yes** |
| Native HTML5 DnD API | More code, no library dep; worse mobile support | No |

**Rationale**: `vue-draggable-plus@0.6.1` is already a dependency. DnD wraps groups/categories/lines independently. On `@end`, extract ordered IDs and call the existing `ReorderCategoryGroups`, `ReorderCategories`, `ReorderBudgetLines` endpoints (`List<Guid> OrderedIds` shape).

### Decision: Summary footer -- Total row approach

| Option | Tradeoff | Chosen |
|--------|----------|--------|
| New `MatrixTotalRow` component summing 3 `MatrixSummaryRow` values | Clean separation; reuses same store accessors | **Yes** |
| Extend `MatrixSummaryRow` with a `total` mode | Overloads one component with two behaviors | No |

**Rationale**: A dedicated `MatrixTotalRow` keeps `MatrixSummaryRow` single-purpose. The total row sums all `budgetLines` budgeted and all `categoryTotals` executed across lineTypes.

### Decision: OperationDate column type

| Option | Tradeoff | Chosen |
|--------|----------|--------|
| `DateOnly?` (nullable) | Safe additive; no default needed; existing records get null | **Yes** |
| `DateOnly` with default `DateOnly.FromDateTime(DateTime.UtcNow)` | Forces a value retroactively; migration must compute defaults | No |

**Rationale**: Nullable is safest for existing data. Frontend defaults to today on new records; null is acceptable for historical records.

### Decision: Inline add-line category dropdown

| Option | Tradeoff | Chosen |
|--------|----------|--------|
| Add `<select>` in inline row, filtered by parent group's categories | Minimal UI; consistent with `BudgetLineModal` filter logic | **Yes** |
| Open full `BudgetLineModal` instead of inline | More clicks; disrupts matrix flow | No |

## Data Flow

### Currency fix flow

```
BudgetLineModal (form.currencyId: Guid)
    |
    v
CreateBudgetLinePayload { currencyId: string(Guid) }
    |
    v
POST /api/.../budget-lines  { CurrencyId: Guid? }
    |
    v (read path)
ListBudgetLinesQuery -> BudgetLineResponse { CurrencyId: Guid? }  <-- NEW field
    |
    v
BudgetLineModal (edit): pre-populates currencyId from response
```

### DnD reorder flow

```
User drags group/category/line
    |
    v
vue-draggable-plus @end event
    |
    v
Extract ordered IDs from DOM model
    |
    v
Call existing store reorder action (e.g. structureStore.reorderGroups)
    |
    v
PUT /api/.../reorder { OrderedIds: Guid[] }
```

### OperationDate flow

```
ExecutionRecordForm (form.operationDate: string YYYY-MM-DD, default=today)
    |
    v
CreateExecutionRequest { operationDate?: string }
    |
    v
CreateExecutionRecordCommand { OperationDate: DateOnly? }
    |
    v
ExecutionRecord.Create(..., operationDate)
    |
    v
EF Core -> DB column [OperationDate DateOnly? nullable]
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Project/frontend/src/features/budget-structure/types.ts` | Modify | Rename `currency` to `currencyId` in `CreateBudgetLinePayload`/`UpdateBudgetLinePayload`; add `currencyId?` to `BudgetLineResponse` |
| `Project/frontend/src/features/budget-structure/components/BudgetLineModal.vue` | Modify | Change `form.currency` to `form.currencyId`; populate from cycle currencies by Guid; send Guid in payload |
| `Project/src/.../ListBudgetLines/ListBudgetLinesQuery.cs` | Modify | Add `Guid? CurrencyId` to `BudgetLineResponse` record |
| `Project/src/.../ListBudgetLines/ListBudgetLinesHandler.cs` | Modify | Map `CurrencyId` from entity to response |
| `Project/frontend/src/features/budget-execution/components/MatrixGroupRow.vue` | Modify | Add `window.getSelection()?.removeAllRanges()` in `startEdit()` |
| `Project/frontend/src/features/budget-execution/components/MatrixCategoryRow.vue` | Modify | Add `window.getSelection()?.removeAllRanges()` in `startEdit()` |
| `Project/frontend/src/features/budget-execution/components/MatrixLineRow.vue` | Modify | Add `window.getSelection()?.removeAllRanges()` in `openEditModal()` |
| `Project/frontend/src/features/budget-execution/views/BudgetMatrixView.vue` | Modify | Reorder footer rows (1,3,2 -> 1,3,2 stays but labels change); rename labels to SubTotal; add `MatrixTotalRow`; add category `<select>` in inline add-line row; wrap group/category/line sections with `vue-draggable-plus` |
| `Project/frontend/src/features/budget-execution/components/MatrixTotalRow.vue` | Create | Total row component summing all 3 lineType subtotals |
| `Project/frontend/src/features/budget-execution/components/MatrixSummaryRow.vue` | Modify | No logic change; label prop already external (rename happens in parent via i18n key) |
| `Project/frontend/src/i18n/locales/en.json` | Modify | Rename summary keys to SubTotal; add Total key |
| `Project/frontend/src/i18n/locales/es.json` | Modify | Same i18n changes in Spanish |
| `Project/src/.../SharedKernel/Entities/ExecutionRecord.cs` | Modify | Add `DateOnly? OperationDate` property; update `Create()`/`Update()` signatures |
| `Project/src/.../Migrations/{timestamp}_AddOperationDate.cs` | Create | EF Core migration adding nullable `OperationDate` column |
| `Project/src/.../CreateExecutionRecord/CreateExecutionRecordCommand.cs` | Modify | Add `DateOnly? OperationDate` parameter |
| `Project/src/.../CreateExecutionRecord/CreateExecutionRecordHandler.cs` | Modify | Pass `OperationDate` to `ExecutionRecord.Create()` |
| `Project/src/.../UpdateExecutionRecord/UpdateExecutionRecordCommand.cs` | Modify | Add `DateOnly? OperationDate` parameter |
| `Project/src/.../UpdateExecutionRecord/UpdateExecutionRecordHandler.cs` | Modify | Pass `OperationDate` to `ExecutionRecord.Update()` |
| `Project/frontend/src/features/budget-execution/types.ts` | Modify | Add `operationDate` to `ExecutionRecordDto`, `CreateExecutionRequest`, `UpdateExecutionRequest` |
| `Project/frontend/src/features/budget-execution/components/ExecutionRecordForm.vue` | Modify | Add `operationDate` (date input, default=today), `currencyId` (select from cycle currencies), `exchangeRate` (number input) fields |

## Interfaces / Contracts

```typescript
// types.ts -- currency fix
export interface CreateBudgetLinePayload {
  // ... existing fields ...
  currencyId?: string  // was: currency?: string
}

export interface BudgetLineResponse {
  // ... existing fields ...
  currencyId?: string  // NEW -- Guid as string
}
```

```typescript
// execution types.ts -- OperationDate
export interface ExecutionRecordDto {
  // ... existing fields ...
  operationDate: string | null  // YYYY-MM-DD or null
}

export interface CreateExecutionRequest {
  // ... existing fields ...
  operationDate?: string | null  // YYYY-MM-DD
}
```

```csharp
// BudgetLineResponse -- backend
public sealed record BudgetLineResponse(
    // ... existing fields ...
    Guid? CurrencyId  // NEW
);

// ExecutionRecord entity -- new property + updated factory
public DateOnly? OperationDate { get; private set; }
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Manual | Currency saves as Guid; edit pre-populates | Create/edit budget line, verify DB |
| Manual | DnD reorder persists | Drag groups/categories/lines, reload, verify order |
| Manual | Footer SubTotal labels + Total row sums | Visual inspection across periods |
| Manual | STATUS_BREAKPOINT no longer triggers | Double-click with text selected in each row type |
| Manual | OperationDate defaults to today, saves, reads | Create/edit execution record |
| Manual | Inline add-line category dropdown filters by group | Click + on category, verify dropdown |

## Migration / Rollout

Single EF Core migration: `AddOperationDateToExecutionRecord`. Adds nullable `DateOnly` column. `Down()` drops the column. No data backfill needed. Safe for populated databases.

## Open Questions

- None -- all decisions confirmed by user in proposal phase.
