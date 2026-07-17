# Tasks: Budget Structure UI E2E Test Debt

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 420–520 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Phase 1) → PR 2 (Phase 2+3 helpers) → PR 3 (Phase 3 spec blocks) → PR 4 (Phase 4) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | i18n keys + view toast insertions | PR 1 | Base = main; self-contained frontend fix |
| 2 | helpers.ts — `expectToast` + 5 `seedDeleted*` | PR 2 | Base = PR 1; E2E infra |
| 3 | Retrofit toast assertions into 4 spec files | PR 3 | Base = PR 2; depends on PR 1 toasts firing |
| 4 | New soft-delete/restore describe blocks in 4 spec files | PR 4 | Base = PR 3; depends on PR 2 helpers |

---

## Phase 1: i18n Keys + View Toast Insertions

- [x] 1.1 `src/i18n/locales/en.json` — add 7 keys: `cycles.updateSuccess`, `cycles.setActiveSuccess`, `periods.updateSuccess`, `periods.statusSuccess`, `categoryGroups.updateSuccess`, `categories.updateSuccess`, `budgetLines.updateSuccess` under `budgetStructure`
- [x] 1.2 `src/i18n/locales/es.json` — add 7 matching Spanish translations (see design §i18n Keys table)
- [x] 1.3 `src/features/budget-structure/views/CycleListView.vue` — add `toastStore.push(...)` with `updateSuccess` in `handleInlineSave` after `inlineEditingCycleId.value = null` (L275)
- [x] 1.4 `src/features/budget-structure/views/CycleListView.vue` — add `toastStore.push(...)` with `setActiveSuccess` in `handleSetActive` after `store.setActiveCycle(...)` (L312)
- [x] 1.5 `src/features/budget-structure/views/CycleListView.vue` — add `toastStore.push(...)` with `updateSuccess` in `handleFormSubmit` edit branch after `store.updateCycle(...)` (L324)
- [x] 1.6 `src/features/budget-structure/views/CycleDetailView.vue` — add `toastStore.push(...)` with `updateSuccess` in `handleInlineSave` after `inlineEditingPeriodId.value = null` (L332)
- [x] 1.7 `src/features/budget-structure/views/CycleDetailView.vue` — add `toastStore.push(...)` with `statusSuccess` in `handleStatusChange` after `store.patchPeriodStatus(...)`
- [x] 1.8 `src/features/budget-structure/views/CycleDetailView.vue` — add `toastStore.push(...)` with `updateSuccess` in `handleFormSubmit` edit branch after `store.updatePeriod(...)`
- [x] 1.9 `src/features/budget-structure/views/CategoryTreeView.vue` — add `toastStore.push(...)` with group `updateSuccess` in inline group save handler (L334) and `handleGroupFormSubmit` edit branch (L374)
- [x] 1.10 `src/features/budget-structure/views/CategoryTreeView.vue` — add `toastStore.push(...)` with category `updateSuccess` in inline category save (L349) and `handleCategoryFormSubmit` edit branch (L429)
- [x] 1.11 `src/features/budget-structure/views/BudgetLinesView.vue` — add `toastStore.push(...)` with `updateSuccess` in `handleInlineSave` (L403) and `handleFormSubmit` edit branch (L387)

---

## Phase 2: E2E Helpers — `expectToast` + `seedDeleted*`

- [x] 2.1 `e2e/budget-structure/helpers.ts` — export `expectToast(page: Page, text: string): Promise<void>` using `page.getByRole('alert').filter({ hasText: text })` with `toBeVisible({ timeout: 5_000 })`
- [x] 2.2 `e2e/budget-structure/helpers.ts` — export `seedDeletedCycle(page, budgetId, token): Promise<string>` — POST create (assert 201) then DELETE (assert 204), return `id`
- [x] 2.3 `e2e/budget-structure/helpers.ts` — export `seedDeletedPeriod(page, budgetId, cycleId, token): Promise<string>`
- [x] 2.4 `e2e/budget-structure/helpers.ts` — export `seedDeletedCategoryGroup(page, budgetId, token): Promise<string>`
- [x] 2.5 `e2e/budget-structure/helpers.ts` — export `seedDeletedCategory(page, budgetId, groupId, token): Promise<string>`
- [x] 2.6 `e2e/budget-structure/helpers.ts` — export `seedDeletedBudgetLine(page, budgetId, periodId, token): Promise<string>`

---

## Phase 3: Retrofit Toast Assertions into Existing CRUD Tests

- [x] 3.1 `e2e/budget-structure/budget-structure-cycles.spec.ts` — in `'create cycle → edit → set active → delete'`: add `await expectToast(page, 'Cycle created successfully')` after create, `'Cycle updated successfully'` after edit, `'Cycle set as active'` after set-active, `'Cycle deleted successfully'` after delete (REQ-E2E-TOAST-1)
- [x] 3.2 `e2e/budget-structure/budget-structure-periods.spec.ts` — in `'create period → change status → delete'`: add `expectToast` for `'Period created successfully'`, `'Period status updated'`, `'Period deleted successfully'` (REQ-E2E-TOAST-2)
- [x] 3.3 `e2e/budget-structure/budget-structure-categories.spec.ts` — in `'create group → add categories → delete category → delete group'`: add `expectToast` for 4 operations (REQ-E2E-TOAST-3)
- [x] 3.4 `e2e/budget-structure/budget-structure-lines.spec.ts` — in `'create line → edit via dblclick → delete'`: add `expectToast` for 3 operations (REQ-E2E-TOAST-4)

---

## Phase 4: New Soft-Delete / Restore Describe Blocks

- [ ] 4.1 `e2e/budget-structure/budget-structure-cycles.spec.ts` — add `test.describe('soft-delete / restore')` with: toggle ON reveals deleted cycle (SCENARIO-TOGGLE-3.1), toggle OFF hides it (SCENARIO-TOGGLE-3.2), restore returns cycle to active list with toast (SCENARIO-RESTORE-3.1)
- [ ] 4.2 `e2e/budget-structure/budget-structure-periods.spec.ts` — add `test.describe('soft-delete / restore')` with: toggle ON/OFF for period (SCENARIO-TOGGLE-3.3, 3.4), restore confirm path (SCENARIO-RESTORE-3.2), restore cancel path (SCENARIO-RESTORE-3.3)
- [ ] 4.3 `e2e/budget-structure/budget-structure-categories.spec.ts` — add `test.describe('soft-delete / restore')` with: toggle ON/OFF for group (SCENARIO-TOGGLE-3.5) and category (3.6), restore group (SCENARIO-RESTORE-3.4), restore category (SCENARIO-RESTORE-3.5)
- [ ] 4.4 `e2e/budget-structure/budget-structure-lines.spec.ts` — add `test.describe('soft-delete / restore')` with: toggle ON/OFF for budget line (SCENARIO-TOGGLE-3.7), restore line (SCENARIO-RESTORE-3.6)
