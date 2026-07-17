# Design: soft-delete-ux

## Technical Approach

Separate ephemeral toast system (new `useToastStore` + `AppToast.vue`) completely decoupled from the persistent notification bell. Show-deleted toggles use per-store refs in `useBudgetStructureStore` (structure entities) and existing `useBudgetExecutionStore` refs (execution). Backend adds `RestorePeriod` VSA slice (mirroring `RestoreCycle`) and `includeDeleted` query param to `ListCycles` Dapper SQL. Three chained PRs with no cross-slice dependencies.

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|----------|--------|----------|-----------|
| 1 | Toast system | New `useToastStore` Pinia store + `AppToast.vue` | Extend `useNotificationStore` with `autoDismiss` | Clean separation: bell = persistent inbox, toasts = ephemeral feedback. Adding auto-dismiss to notification store would require a "bell exclusion" flag on every toast push, polluting the notification list. Two small stores are simpler than one overloaded store. |
| 2 | Toast rendering | DaisyUI `toast` + `alert-*` classes, positioned bottom-right, z-50 | Custom CSS / third-party lib | DaisyUI already in use; `toast` class handles positioning; `alert-success`/`alert-error` for type styling. No new dependency. |
| 3 | Show-deleted state location | Refs inside existing stores (`useBudgetStructureStore` for cycles/periods/groups/categories/lines) | Shared `useShowDeletedState()` composable | Keeps state co-located with the data it filters. Structure store already owns `cycles`, `periods`, `categoryGroups`, `budgetLines` refs. Execution store already has `showDeleted` + `showDeletedInModal`. No new composable needed. |
| 4 | Cycles includeDeleted | Conditional SQL branch in `ListCyclesHandler` Dapper query | EF global filter | ListCycles uses raw Dapper, not EF. Add `bool IncludeDeleted` to `ListCyclesQuery`; if false keep existing `WHERE c."DeletedAt" IS NULL`, if true omit that condition but add `c."DeletedAt"` to SELECT for frontend row styling. |
| 5 | Period restore cascade | Warning text rendered inline above the restore button (same two-step pattern as MatrixLineRow) | Modal dialog | Consistent with existing two-step confirm pattern used in matrix rows. No new dialog infrastructure needed. Warning text: "This will also restore N budget lines." |
| 6 | ExecutionRecord confirm | Two-step inline button in `ExecutionRecordRow` (same `confirmingDelete` ref pattern as `MatrixLineRow`) | Nested `<dialog>` | Matches existing codebase pattern. Dialog is reserved as post-impl fallback per product decision #4. |
| 7 | RestorePeriod endpoint | `POST /api/budgets/{id}/cycles/{cycleId}/periods/{periodId}/restore` with `?includeExecutionRecords` | `POST /api/periods/{id}/restore` flat route | Matches existing nested route convention from `DeletePeriodEndpoint` (`/api/budgets/{id}/cycles/{cycleId}/periods/{periodId}`). Consistent with `RestoreCycle` having `?includeExecutionRecords`. |

## Data Flow

### Toast lifecycle

```
User action (delete/restore)
  -> API call succeeds
  -> toastStore.push({ type, message })
  -> AppToast.vue renders stacked alert
  -> setTimeout(3000) -> toastStore.dismiss(id)
  -> User can also click x -> toastStore.dismiss(id)
```

### Show-deleted toggle (Cycles example)

```
User toggles showDeletedCycles in CycleListView
  -> structureStore.showDeletedCycles = true
  -> structureStore.loadCycles(budgetId, includeDeleted: true)
  -> cyclesApi.list(budgetId, { includeDeleted: true })
  -> GET /api/budgets/{id}/cycles?includeDeleted=true
  -> ListCyclesHandler branches SQL (omits DeletedAt IS NULL)
  -> Response includes deleted cycles with deletedAt field
  -> CycleListView renders deleted rows with opacity-60 + restore button
```

### Period restore cascade

```
User clicks Restore on deleted period (CycleDetailView)
  -> Two-step: first click shows warning "Will restore period + N budget lines"
  -> Second click confirms
  -> periodsApi.restore(budgetId, cycleId, periodId)
  -> POST /api/budgets/{id}/cycles/{cycleId}/periods/{periodId}/restore
  -> RestorePeriodHandler: restore period + child BudgetLines
  -> 204 No Content
  -> Reload period list + toast "Period restored"
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `frontend/src/stores/toast.store.ts` | Create | Pinia store: `toasts` ref, `push(type, message)`, `dismiss(id)`, auto-dismiss timer |
| `frontend/src/components/AppToast.vue` | Create | Renders stacked toasts bottom-right, DaisyUI `toast` + `alert-*`, close button, transition |
| `frontend/src/layouts/AppLayout.vue` | Modify | Mount `<AppToast />` at root level |
| `frontend/src/features/budget-structure/api/cycles.api.ts` | Modify | Add `includeDeleted` param to `list()`, add `restore()` function |
| `frontend/src/features/budget-structure/api/periods.api.ts` | Modify | Add `restore()` function |
| `frontend/src/features/budget-structure/store.ts` | Modify | Add `showDeletedCycles`, `showDeletedPeriods`, `showDeletedGroups`, `showDeletedCategories`, `showDeletedLines` refs + toggle/reload actions + restore actions |
| `frontend/src/features/budget-structure/views/CycleListView.vue` | Modify | Show-deleted toggle, deleted row styling, restore button |
| `frontend/src/features/budget-structure/views/CycleDetailView.vue` | Modify | Show-deleted toggle for periods, deleted row styling, restore button with cascade warning |
| `frontend/src/features/budget-structure/views/CategoryTreeView.vue` | Modify | Show-deleted toggle, deleted row styling, restore button |
| `frontend/src/features/budget-structure/views/BudgetLinesView.vue` | Modify | Show-deleted toggle, deleted row styling, restore button |
| `frontend/src/features/budget-execution/components/ExecutionRecordRow.vue` | Modify | Add two-step `confirmingDelete` pattern to delete button |
| `frontend/src/i18n/locales/en.json` | Modify | Add ~20 keys across entity namespaces |
| `frontend/src/i18n/locales/es.json` | Modify | Add ~20 keys across entity namespaces |
| `src/MyBudget.Features/.../ListCycles/ListCyclesQuery.cs` | Modify | Add `bool IncludeDeleted` field |
| `src/MyBudget.Features/.../ListCycles/ListCyclesEndpoint.cs` | Modify | Bind `includeDeleted` query param |
| `src/MyBudget.Features/.../ListCycles/ListCyclesHandler.cs` | Modify | Conditional SQL: include/exclude deleted cycles; add `DeletedAt` to SELECT |
| `src/MyBudget.Features/.../RestorePeriod/RestorePeriodCommand.cs` | Create | Command record (BudgetId, CycleId, PeriodId, IncludeExecutionRecords) |
| `src/MyBudget.Features/.../RestorePeriod/RestorePeriodEndpoint.cs` | Create | POST endpoint with `budget:admin` auth |
| `src/MyBudget.Features/.../RestorePeriod/RestorePeriodHandler.cs` | Create | Restore period + cascade child BudgetLines (+ ExecutionRecords if flag) |
| `src/MyBudget.Features/.../RestorePeriod/RestorePeriodValidator.cs` | Create | FluentValidation: BudgetId, CycleId, PeriodId NotEmpty |
| `tests/.../RestorePeriod/RestorePeriodHandlerTests.cs` | Create | Unit tests mirroring RestoreCycleHandlerTests |

## Interfaces / Contracts

```typescript
// toast.store.ts
interface Toast {
  id: string
  type: 'success' | 'error' | 'info' | 'warning'
  message: string
}
// push(type, message) -> adds toast, starts 3s timer
// dismiss(id) -> removes toast immediately
```

```typescript
// cycles.api.ts additions
function list(budgetId: string, opts?: { includeDeleted?: boolean }): Promise<CycleListItem[]>
function restore(budgetId: string, cycleId: string, includeExecutionRecords?: boolean): Promise<void>
```

```typescript
// periods.api.ts addition
function restore(budgetId: string, cycleId: string, periodId: string, includeExecutionRecords?: boolean): Promise<void>
```

```csharp
// RestorePeriodCommand.cs
public sealed record RestorePeriodCommand(
    Guid BudgetId, Guid CycleId, Guid PeriodId,
    bool IncludeExecutionRecords) : IRequest<Result<Guid>>;
```

```csharp
// ListCyclesQuery.cs (modified)
public sealed record ListCyclesQuery(
    Guid BudgetId,
    bool IncludeDeleted = false) : IRequest<Result<IReadOnlyList<CycleListItem>>>;
// CycleListItem gains: DateTimeOffset? DeletedAt
```

## i18n Key Locations

| Namespace | New keys |
|-----------|----------|
| `budgetStructure.cycles` | `deleteSuccess`, `restoreSuccess`, `showDeleted`, `confirmRestore` |
| `budgetStructure.periods` | `deleteSuccess`, `restoreSuccess`, `showDeleted`, `confirmRestore`, `cascadeWarning` |
| `budgetStructure.categoryGroups` | `deleteSuccess`, `restoreSuccess`, `showDeleted` |
| `budgetStructure.categories` | `deleteSuccess`, `restoreSuccess` |
| `budgetStructure.budgetLines` | `deleteSuccess`, `restoreSuccess`, `showDeleted` |
| `budgetExecution.row` | `deleteSuccess`, `restoreSuccess`, `confirmDeleteStep` |

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `RestorePeriodHandler` cascade logic | xUnit, mock `AppDbContext`, verify period + lines restored |
| Unit | `ListCyclesHandler` includeDeleted branch | xUnit, in-memory DB, verify deleted cycles included/excluded |
| Unit | `useToastStore` push/dismiss/auto-dismiss | Vitest, fake timers |
| Component | `AppToast.vue` rendering + close button | Vitest + @vue/test-utils |
| Component | `ExecutionRecordRow` two-step delete | Vitest + @vue/test-utils, verify state transitions |
| E2E | Delete + toggle + restore cycle flow | Playwright (if existing E2E coverage warrants it) |

## PR Slice Boundaries

**PR 1 -- Toast infrastructure + i18n** (~120 lines)
- `toast.store.ts`, `AppToast.vue`, `AppLayout.vue` mount
- Wire success toasts on Budget delete/restore in `BudgetSelectionView` as proof
- All i18n keys for all entities (en.json + es.json)
- No cross-dependency: purely additive

**PR 2 -- Structure entity soft-delete UX** (~250 lines)
- Backend: `RestorePeriod` VSA slice (4 files), `ListCycles` includeDeleted (3 files modified)
- Frontend: show-deleted toggles + restore in CycleListView, CycleDetailView, CategoryTreeView, BudgetLinesView
- Period cascade warning
- Depends on PR 1 for toast store (chained PR target)

**PR 3 -- ExecutionRecord confirmation + cleanup** (~80 lines)
- `ExecutionRecordRow` two-step delete pattern
- Success toasts on ExecutionRecord delete/restore
- Depends on PR 1 for toast store (chained PR target)
- Post-impl UX review checkpoint

## Migration / Rollout

No migration required. No database schema changes. All changes are additive query-level backend + frontend UI.

## Open Questions

- None blocking. Decision #4 (ExecutionRecord two-step vs dialog) is provisional with a post-impl review checkpoint in PR 3.
