# Design: Budget Structure UI E2E Test Debt

## Technical Approach

Close 7 missing toast implementations in view files, add i18n keys (en + es), then retrofit toast assertions into 4 existing CRUD E2E tests and add new soft-delete/restore E2E describe blocks with API-based seed helpers. No backend changes. All toast calls follow the existing `toastStore.push({ type: 'success', title: t('...') })` pattern already established in create/delete/restore handlers.

## Architecture Decisions

| Decision | Choice | Rejected | Rationale |
|----------|--------|----------|-----------|
| Toast assertion selector | `page.getByRole('alert')` filtered by `hasText` | `data-testid`, CSS class | `role="alert"` already rendered by AppToast.vue (line 35); no markup changes needed |
| Toggle selector | `page.getByLabel('Show deleted')` | `getByRole('switch')`, `getByTestId` | Toggle is `<input type="checkbox">` + `<label>` in all 4 views; `getByLabel` uses existing label text from i18n; `getByRole('switch')` would fail (no switch role rendered) |
| Restore button selector | `page.getByRole('button', { name: 'Restore' })` scoped to row | `getByTestId` | Restore button text is `budgetStructure.common.restore` = "Restore"; consistent with existing test patterns using `getByRole('button')` |
| Period cascade confirm/cancel | `page.getByRole('button', { name: 'Confirm' })` / `{ name: 'Cancel' }` inside `dialog.modal-open` | Dialog testid | Matches existing delete-confirm pattern in all spec files |
| Toast helper | `expectToast(page, text)` in helpers.ts | Inline assertions everywhere | Reduces 20+ copy-paste assertions to 1-liner; encapsulates timeout + role + text filter |
| Seed helper auth | Extract token from `localStorage` inside each seed function via `page.evaluate` | Pass token as parameter | Existing `budget-structure-lines.spec.ts` already uses this `page.evaluate(() => localStorage.getItem('accessToken'))` pattern; however, spec REQ-SEED-1 defines `token` as a parameter for clarity and testability, so we follow the spec |
| es.json update | In scope | Out of scope per spec section 8 | Spec says "Locale files other than en.json" are out of scope, but es.json already has all existing keys; leaving gaps would break Spanish locale. Include es.json for consistency |

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/i18n/locales/en.json` | Modify | Add 7 new i18n keys under `budgetStructure.{entity}` |
| `src/i18n/locales/es.json` | Modify | Add 7 matching Spanish i18n keys |
| `src/features/budget-structure/views/CycleListView.vue` | Modify | Add toast in `handleInlineSave` (updateSuccess), `handleSetActive` (setActiveSuccess), `handleFormSubmit` edit branch (updateSuccess) |
| `src/features/budget-structure/views/CycleDetailView.vue` | Modify | Add toast in `handleInlineSave` (updateSuccess), `handleStatusChange` (statusSuccess), `handleFormSubmit` edit branch (updateSuccess) |
| `src/features/budget-structure/views/CategoryTreeView.vue` | Modify | Add toast in `handleGroupInlineSave` (updateSuccess), `handleCategoryInlineSave` (updateSuccess), `handleGroupFormSubmit` edit branch (updateSuccess), `handleCategoryFormSubmit` edit branch (updateSuccess) |
| `src/features/budget-structure/views/BudgetLinesView.vue` | Modify | Add toast in `handleInlineSave` (updateSuccess), `handleFormSubmit` edit branch (updateSuccess) |
| `e2e/budget-structure/helpers.ts` | Modify | Add `expectToast` helper + 5 `seedDeleted*` functions |
| `e2e/budget-structure/budget-structure-cycles.spec.ts` | Modify | Retrofit 4 toast assertions + new `soft-delete / restore` describe |
| `e2e/budget-structure/budget-structure-periods.spec.ts` | Modify | Retrofit 3 toast assertions + new `soft-delete / restore` describe with cascade confirm/cancel |
| `e2e/budget-structure/budget-structure-categories.spec.ts` | Modify | Retrofit 4 toast assertions + new `soft-delete / restore` describe for group + category |
| `e2e/budget-structure/budget-structure-lines.spec.ts` | Modify | Retrofit 3 toast assertions + new `soft-delete / restore` describe |

## i18n Keys

| Entity | Operation | Key | English | Spanish |
|--------|-----------|-----|---------|---------|
| Cycle | edit | `budgetStructure.cycles.updateSuccess` | Cycle updated successfully | Ciclo actualizado exitosamente |
| Cycle | set-active | `budgetStructure.cycles.setActiveSuccess` | Cycle set as active | Ciclo establecido como activo |
| Period | edit | `budgetStructure.periods.updateSuccess` | Period updated successfully | Periodo actualizado exitosamente |
| Period | status | `budgetStructure.periods.statusSuccess` | Period status updated | Estado del periodo actualizado |
| CategoryGroup | edit | `budgetStructure.categoryGroups.updateSuccess` | Category group updated successfully | Grupo de categorias actualizado exitosamente |
| Category | edit | `budgetStructure.categories.updateSuccess` | Category updated successfully | Categoria actualizada exitosamente |
| BudgetLine | edit | `budgetStructure.budgetLines.updateSuccess` | Budget line updated successfully | Linea de presupuesto actualizada exitosamente |

## Selector Strategy

| Element | Locator | Rationale |
|---------|---------|-----------|
| Toast | `page.getByRole('alert').filter({ hasText: '...' })` | AppToast renders `role="alert"` per toast; filter by text to disambiguate multiple |
| Show-deleted toggle | `page.getByLabel('Show deleted')` | All views use `<input type="checkbox">` + `<label>Show deleted</label>` pattern |
| Restore button | `page.getByRole('button', { name: 'Restore' })` | All views render `<button>...Restore</button>` from `budgetStructure.common.restore` |
| Period cascade dialog Confirm | `page.getByRole('button', { name: 'Confirm' })` | Dialog uses same Confirm/Cancel pattern as delete dialogs |
| Period cascade dialog Cancel | `page.getByRole('button', { name: 'Cancel' })` | Same dialog pattern |

## Interfaces / Contracts

### Toast assertion helper

```typescript
// e2e/budget-structure/helpers.ts
export async function expectToast(page: Page, text: string): Promise<void> {
  await expect(
    page.getByRole('alert').filter({ hasText: text })
  ).toBeVisible({ timeout: 5_000 })
}
```

### Seed helper signatures

```typescript
// e2e/budget-structure/helpers.ts
export async function seedDeletedCycle(page: Page, budgetId: string, token: string): Promise<string>
export async function seedDeletedPeriod(page: Page, budgetId: string, cycleId: string, token: string): Promise<string>
export async function seedDeletedCategoryGroup(page: Page, budgetId: string, token: string): Promise<string>
export async function seedDeletedCategory(page: Page, budgetId: string, groupId: string, token: string): Promise<string>
export async function seedDeletedBudgetLine(page: Page, budgetId: string, periodId: string, token: string): Promise<string>
```

Each: POST to create (assert 201) -> DELETE to soft-delete (assert 204) -> return entity `id`.

## Toast call insertion points

| View | Function | Insert after | Code to add |
|------|----------|-------------|-------------|
| CycleListView | `handleInlineSave` | `inlineEditingCycleId.value = null` (L275) | `toastStore.push(...)` with `updateSuccess` |
| CycleListView | `handleSetActive` | `store.setActiveCycle(...)` (L312) | `toastStore.push(...)` with `setActiveSuccess` |
| CycleListView | `handleFormSubmit` edit branch | `store.updateCycle(...)` (L324) | `toastStore.push(...)` with `updateSuccess` |
| CycleDetailView | `handleInlineSave` | `inlineEditingPeriodId.value = null` (L332) | `toastStore.push(...)` with `updateSuccess` |
| CycleDetailView | `handleStatusChange` | `store.patchPeriodStatus(...)` | `toastStore.push(...)` with `statusSuccess` |
| CycleDetailView | `handleFormSubmit` edit branch | `store.updatePeriod(...)` | `toastStore.push(...)` with `updateSuccess` |
| CategoryTreeView | inline group save (L334) | `store.updateGroup(...)` | `toastStore.push(...)` with group `updateSuccess` |
| CategoryTreeView | inline category save (L349) | `store.updateCategory(...)` | `toastStore.push(...)` with category `updateSuccess` |
| CategoryTreeView | `handleGroupFormSubmit` edit branch (L374) | `store.updateGroup(...)` | `toastStore.push(...)` with group `updateSuccess` |
| CategoryTreeView | `handleCategoryFormSubmit` edit branch (L429) | `store.updateCategory(...)` | `toastStore.push(...)` with category `updateSuccess` |
| BudgetLinesView | `handleInlineSave` (L403) | `store.updateLine(...)` | `toastStore.push(...)` with `updateSuccess` |
| BudgetLinesView | `handleFormSubmit` edit branch (L387) | `store.updateLine(...)` | `toastStore.push(...)` with `updateSuccess` |

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| E2E | Toast appears for all CRUD ops | Retrofit `expectToast()` into existing tests (Phase 2) |
| E2E | Toggle show/hide deleted items | New describe blocks with `seedDeleted*` setup (Phase 3) |
| E2E | Restore returns item to active list | New tests within same describe blocks (Phase 3) |
| E2E | Period restore cascade confirm/cancel | Two tests: confirm path + cancel path (Phase 3) |

## Phase Execution Order

1. **Phase 1** (toast implementation): Add i18n keys + toast calls in views. Must come first because Phase 2 assertions depend on toasts actually firing.
2. **Phase 2** (retrofit): Add `expectToast` helper to helpers.ts, then insert toast assertions into existing CRUD tests. Must come before Phase 3 because the helper is shared.
3. **Phase 3** (soft-delete/restore E2E): Add `seedDeleted*` helpers, then new describe blocks. Depends on Phase 2 helper and Phase 1 toast calls.

## Migration / Rollout

No migration required. All changes are additive (new i18n keys, new toast calls after existing success paths, new E2E tests and helpers).

## Open Questions

None. All selectors, patterns, and insertion points are confirmed from codebase inspection.
