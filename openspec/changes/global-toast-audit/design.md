# Design: Global Toast Audit

## Technical Approach

Add `useToastStore()` + `toast.push({ type: 'success', title: t('<key>') })` to 13 silent mutation call sites across 5 Vue components, migrate `ChangePasswordModal` from `notificationStore` to `toastStore`, and add 8 new i18n keys to both locales. Pure additive -- no store shape changes, no new components, no API changes.

## Architecture Decisions

### Decision: Toast placement inside try-block success path, never in finally

| Option | Tradeoff | Chosen? |
|--------|----------|---------|
| Inside `try` after successful `await` | Toast only on success; silent on error | **Yes** |
| In `finally` block | Toast fires even on failure | No |
| After `try/finally` | Same as finally -- unreachable on throw only if re-thrown | No |

**Rationale**: Existing codebase pattern (`BudgetSelectionView` delete/restore) places toast inside `try` after the awaited call succeeds. Matrix row functions (`doDelete`, `doRestore`) have `try/finally` with `acting.value = false` in `finally` -- toast goes inside `try`, after the store action and before state cleanup.

### Decision: Shared delete/restore keys for matrix rows

| Option | Tradeoff | Chosen? |
|--------|----------|---------|
| Per-entity keys (`deleteGroupSuccess`, `deleteCategorySuccess`, `deleteLineSuccess`) | More specific, 6 extra keys | No |
| Shared keys (`deleteSuccess`, `restoreSuccess`) | Consistent with generic confirmation UX; fewer keys | **Yes** |

**Rationale**: The matrix row delete/restore confirmations already use generic text (`budgetMatrix.rows.confirmDelete`). Shared success keys match. Create/update remain entity-specific because the feedback is meaningful.

### Decision: ChangePasswordModal migration to toastStore

| Option | Tradeoff | Chosen? |
|--------|----------|---------|
| Keep `notificationStore` | Inconsistency persists; notifications go to bell, not ephemeral toast | No |
| Migrate to `toastStore` | Aligns with codebase pattern; 2-line change | **Yes** |

**Rationale**: `notificationStore` feeds the bell/notification panel (persistent, read/unread). `toastStore` feeds ephemeral auto-dismiss feedback. Password change success is ephemeral feedback, not a persistent notification.

## Data Flow

```
User action (click/submit)
    |
    v
Component function (confirmAddGroup, saveEdit, doDelete, etc.)
    |
    v
structureStore.{action}()  -- await API call
    |
    v  (on success, still inside try)
toast.push({ type: 'success', title: t('i18n.key') })
    |
    v
AppToast renders ephemeral toast (auto-dismiss)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `frontend/src/features/budget-execution/views/BudgetMatrixView.vue` | Modify | Add `useToastStore` import + `const toast`, add `toast.push()` in `confirmAddGroup`, `confirmAddCategory`, `confirmAddLine` |
| `frontend/src/features/budget-execution/components/MatrixGroupRow.vue` | Modify | Add `useToastStore` import + `const toast`, add `toast.push()` in `saveEdit`, `doDelete`, `doRestore` |
| `frontend/src/features/budget-execution/components/MatrixCategoryRow.vue` | Modify | Add `useToastStore` import + `const toast`, add `toast.push()` in `saveEdit`, `doDelete`, `doRestore` |
| `frontend/src/features/budget-execution/components/MatrixLineRow.vue` | Modify | Add `useToastStore` import + `const toast`, add `toast.push()` in `doDelete`, `doRestore` |
| `frontend/src/features/budget-structure/views/BudgetSelectionView.vue` | Modify | Add `toast.push()` in `onBudgetCreated` (after `selectBudget`), add `toast.push()` in `saveInlineEdit` (inside `try`) |
| `frontend/src/components/auth/ChangePasswordModal.vue` | Modify | Replace `useNotificationStore` import with `useToastStore`, replace `notificationStore.push(...)` with `toast.push({ type: 'success', title: t('auth.password.changeSuccess') })` |
| `frontend/src/i18n/locales/en.json` | Modify | Add 8 keys (see i18n section) |
| `frontend/src/i18n/locales/es.json` | Modify | Add 8 keys (mirror) |

## Per-File Change Specification

### BudgetMatrixView.vue

**Add import**: `import { useToastStore } from '@/stores/toast.store'` (after existing store imports, line ~276)
**Add const**: `const toast = useToastStore()` (after `matrixStore` init, ~line 298)

| Function | Toast placement | i18n key |
|----------|----------------|----------|
| `confirmAddGroup` | After `await matrixStore.invalidateAllPeriods()`, before `addingGroup.value = false` | `budgetMatrix.rows.createGroupSuccess` |
| `confirmAddCategory` | After `await matrixStore.invalidateAllPeriods()`, before `addingCategoryForGroup.value = null` | `budgetMatrix.rows.createCategorySuccess` |
| `confirmAddLine` | After `await matrixStore.invalidateAllPeriods()`, before `addingLineForCategory.value = null` | `budgetMatrix.rows.createLineSuccess` |

### MatrixGroupRow.vue

**Add import**: `import { useToastStore } from '@/stores/toast.store'` (after existing store imports)
**Add const**: `const toast = useToastStore()` (after existing store inits)

| Function | Toast placement | i18n key |
|----------|----------------|----------|
| `saveEdit` | After `await structureStore.updateGroup(...)` | `budgetMatrix.rows.updateGroupSuccess` |
| `doDelete` | Inside `try`, after `await matrixStore.invalidateAllPeriods()` | `budgetMatrix.rows.deleteSuccess` |
| `doRestore` | Inside `try`, after `await matrixStore.invalidateAllPeriods()` | `budgetMatrix.rows.restoreSuccess` |

### MatrixCategoryRow.vue

**Add import**: `import { useToastStore } from '@/stores/toast.store'` (after existing store imports)
**Add const**: `const toast = useToastStore()` (after existing store inits)

| Function | Toast placement | i18n key |
|----------|----------------|----------|
| `saveEdit` | After `await structureStore.updateCategory(...)` | `budgetMatrix.rows.updateCategorySuccess` |
| `doDelete` | Inside `try`, after `await matrixStore.invalidateAllPeriods()` | `budgetMatrix.rows.deleteSuccess` |
| `doRestore` | Inside `try`, after `await matrixStore.invalidateAllPeriods()` | `budgetMatrix.rows.restoreSuccess` |

### MatrixLineRow.vue

**Add import**: `import { useToastStore } from '@/stores/toast.store'` (after existing store imports)
**Add const**: `const toast = useToastStore()` (after existing store inits)

| Function | Toast placement | i18n key |
|----------|----------------|----------|
| `doDelete` | Inside `try`, after `await matrixStore.invalidateAllPeriods()` | `budgetMatrix.rows.deleteSuccess` |
| `doRestore` | Inside `try`, after `await matrixStore.invalidateAllPeriods()` | `budgetMatrix.rows.restoreSuccess` |

### BudgetSelectionView.vue

Already has `useToastStore` imported and `const toastStore`.

| Function | Toast placement | i18n key |
|----------|----------------|----------|
| `onBudgetCreated` | After `selectBudget(budget.id, budget.name)` (post-navigation; store survives) | `budgetStructure.selection.createSuccess` (orphaned key -- already exists) |
| `saveInlineEdit` | Inside `try`, after `inlineEditingBudgetId.value = null` | `budgetStructure.selection.renameSuccess` (new key) |

### ChangePasswordModal.vue

**Replace import**: `useNotificationStore` from `@/stores/notification.store` -> `useToastStore` from `@/stores/toast.store`
**Replace const**: `const notificationStore = useNotificationStore()` -> `const toast = useToastStore()`
**Replace call**: `notificationStore.push({ type: 'success', title: t('auth.password.changeSuccess'), message: '' })` -> `toast.push({ type: 'success', title: t('auth.password.changeSuccess') })`

No new i18n key needed -- `auth.password.changeSuccess` already exists in both locales.

## i18n Keys

### en.json additions

Inside `budgetStructure.selection` (after existing `createSuccess`):
```json
"renameSuccess": "Budget renamed successfully"
```

Inside `budgetMatrix.rows` (after existing `newGroupName`):
```json
"createGroupSuccess": "Group created successfully",
"updateGroupSuccess": "Group updated successfully",
"deleteSuccess": "Item deleted successfully",
"restoreSuccess": "Item restored successfully",
"createCategorySuccess": "Category created successfully",
"updateCategorySuccess": "Category updated successfully",
"createLineSuccess": "Budget line created successfully"
```

### es.json additions

Inside `budgetStructure.selection`:
```json
"renameSuccess": "Presupuesto renombrado correctamente"
```

Inside `budgetMatrix.rows`:
```json
"createGroupSuccess": "Grupo creado correctamente",
"updateGroupSuccess": "Grupo actualizado correctamente",
"deleteSuccess": "Elemento eliminado correctamente",
"restoreSuccess": "Elemento restaurado correctamente",
"createCategorySuccess": "Categoria creada correctamente",
"updateCategorySuccess": "Categoria actualizada correctamente",
"createLineSuccess": "Linea presupuestaria creada correctamente"
```

Total: 8 new keys per locale. 0 new keys for ChangePasswordModal (reuses existing).

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Component | Each modified component fires `toast.push` on success | Vitest + Vue Test Utils: mock `useToastStore`, trigger action, assert `push` called with correct args |
| Component | ChangePasswordModal uses `toastStore` not `notificationStore` | Vitest: verify import changed, mock `toastStore`, assert `push` called after successful submit |
| Integration | i18n keys resolve without warnings | Vitest: load both locale files, assert all 8 new keys exist in both `en.json` and `es.json` |
| E2E | None | Existing E2E patterns cover toast visibility; no new E2E tests for this change |

## Migration / Rollout

No migration required. Pure additive UI feedback -- single commit, single PR.

## Open Questions

None. All infrastructure exists, patterns are established, i18n key structure is clear.
