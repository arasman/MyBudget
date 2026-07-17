# Tasks: soft-delete-ux

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~450 (PR1 ~120, PR2 ~250, PR3 ~80) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 → PR 2 → PR 3 (stacked-to-main) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Toast infra + i18n | PR 1 | Additive only; no existing code broken; base = main |
| 2 | Backend gaps + structure UX | PR 2 | Depends on PR 1 (toast calls); base = PR 1 branch |
| 3 | ExecutionRecord confirm + execution toasts | PR 3 | Depends on PR 1 only; base = PR 1 branch |

---

## PR 1 — Toast Infrastructure + i18n (~120 lines)

### Phase 1: Toast Store

- [ ] PR1.1 Create `frontend/src/stores/toast.store.ts` — Pinia store with `toasts` ref, `push(type, message)` (generates UUID id, sets 3000ms auto-dismiss timer), `dismiss(id)`. Types: `'success' | 'error' | 'info' | 'warning'`. Satisfies REQ-TOAST-1.
- [ ] PR1.2 Write unit test `frontend/src/stores/__tests__/toast.store.spec.ts` — RED/GREEN for push+auto-dismiss, manual dismiss, stacking. Satisfies REQ-TOAST-1 scenarios.

### Phase 2: AppToast Component

- [ ] PR1.3 Create `frontend/src/components/AppToast.vue` — renders `<div class="toast toast-end">` (bottom-right, z-50) iterating `toastStore.toasts`; each item uses DaisyUI `alert alert-{type}` + × close button calling `dismiss(id)`; Vue transition for enter/leave. Satisfies REQ-TOAST-2.
- [ ] PR1.4 Modify `frontend/src/layouts/AppLayout.vue` — import and mount `<AppToast />` once at root. Satisfies REQ-TOAST-2.
- [ ] PR1.5 Write component test `frontend/src/components/__tests__/AppToast.spec.ts` — renders stacked toasts, close button calls dismiss, z-index check. Satisfies REQ-TOAST-2 scenarios.

### Phase 3: Bell Exclusion Verification

- [ ] PR1.6 Add test asserting `useNotificationStore.push` is NOT called when `useToastStore.push` is used — verifies REQ-TOAST-3 (bell count unaffected).

### Phase 4: i18n Keys

- [ ] PR1.7 Modify `frontend/src/i18n/locales/en.json` — add 22 keys under `budgetStructure.{cycles,periods,categoryGroups,categories,budgetLines}.{createSuccess,deleteSuccess,restoreSuccess,showDeleted}` and `budgetExecution.record.{deleteSuccess,restoreSuccess}`. Satisfies REQ-TOAST-I18N-1.
- [ ] PR1.8 Modify `frontend/src/i18n/locales/es.json` — mirror all 22 keys from PR1.7 in Spanish. Satisfies REQ-TOAST-I18N-1.

### Phase 5: Proof-of-Concept Toast Wiring on Budget

- [ ] PR1.9 Modify the Budget delete/restore success handlers (locate in `BudgetSelectionView.vue` or its composable) — call `toastStore.push('success', t('budgetStructure.selection.deleteSuccess'))` / `restoreSuccess` after API success. Validates the toast pipeline end-to-end before PR 2 builds on it.

---

## PR 2 — Structure Entity Soft-Delete UX + Backend (~250 lines)

> Depends on: PR 1 merged to main. Base branch: main (after PR 1 merge).

### Phase 1: Backend — RestorePeriod VSA Slice

- [ ] PR2.1 Create `src/MyBudget.Features/Features/BudgetStructure/RestorePeriod/RestorePeriodCommand.cs` — record `(Guid BudgetId, Guid CycleId, Guid PeriodId, bool IncludeExecutionRecords) : IRequest<Result<Guid>>`. Satisfies REQ-RST-PERIOD-1.
- [ ] PR2.2 Create `src/MyBudget.Features/Features/BudgetStructure/RestorePeriod/RestorePeriodValidator.cs` — FluentValidation: non-empty GUIDs. Satisfies REQ-RST-PERIOD-1.
- [ ] PR2.3 Create `src/MyBudget.Features/Features/BudgetStructure/RestorePeriod/RestorePeriodHandler.cs` — restore Period row (`DeletedAt = null`), cascade restore child BudgetLines; return 404 if Period not soft-deleted or not found; return 409 `PARENT_IS_DELETED` if parent Cycle is soft-deleted. Satisfies REQ-RST-PERIOD-1 (happy path + error cases).
- [ ] PR2.4 Create `src/MyBudget.Features/Features/BudgetStructure/RestorePeriod/RestorePeriodEndpoint.cs` — `POST /api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/restore`, requires `budget:admin`. Satisfies REQ-RST-PERIOD-1 (auth).
- [ ] PR2.5 Create `tests/MyBudget.Features.Tests/BudgetStructure/RestorePeriod/RestorePeriodHandlerTests.cs` — unit tests: happy path (Period + BudgetLines restored), Period not deleted (404), Period not found (404), parent Cycle deleted (409), unauthenticated (401), insufficient role (403). Satisfies REQ-RST-PERIOD-1 scenarios.

### Phase 2: Backend — ListCycles includeDeleted

- [ ] PR2.6 Modify `src/MyBudget.Features/Features/BudgetStructure/ListCycles/ListCyclesQuery.cs` — add `bool IncludeDeleted = false`; add `DateTimeOffset? DeletedAt` to `CycleListItem`. Satisfies REQ-LIST-CYC-DELETED-1.
- [ ] PR2.7 Modify `src/MyBudget.Features/Features/BudgetStructure/ListCycles/ListCyclesHandler.cs` — conditional SQL: if `IncludeDeleted` false keep `WHERE c."DeletedAt" IS NULL`, else omit and SELECT `c."DeletedAt"`. Satisfies REQ-LIST-CYC-DELETED-1.
- [ ] PR2.8 Modify `src/MyBudget.Features/Features/BudgetStructure/ListCycles/ListCyclesEndpoint.cs` — bind `?includeDeleted` query param and pass to query. Satisfies REQ-LIST-CYC-DELETED-1.

### Phase 3: Frontend API Layer

- [ ] PR2.9 Modify `frontend/src/features/budget-structure/api/cycles.api.ts` — add `includeDeleted?: boolean` to `list()` opts; add `restore(budgetId, cycleId, opts?)` function. Satisfies REQ-RESTORE-1 + REQ-LIST-CYC-DELETED-1 frontend side.
- [ ] PR2.10 Modify `frontend/src/features/budget-structure/api/periods.api.ts` — add `restore(budgetId, cycleId, periodId, opts?)` calling `POST .../periods/{periodId}/restore`. Satisfies REQ-RESTORE-PERIOD-1 frontend side.

### Phase 4: Frontend Store

- [ ] PR2.11 Modify `frontend/src/features/budget-structure/store.ts` — add `showDeletedCycles`, `showDeletedPeriods`, `showDeletedCategoryGroups`, `showDeletedCategories`, `showDeletedBudgetLines` refs (default `false`); add `restoreCycle`, `restorePeriod`, `restoreCategoryGroup`, `restoreCategory`, `restoreBudgetLine` actions (each calls API then reloads list + pushes success toast). Satisfies REQ-TOGGLE-1 + REQ-RESTORE-1.

### Phase 5: Frontend Views

- [ ] PR2.12 Modify `frontend/src/features/budget-structure/views/CycleListView.vue` — add show-deleted toggle binding `showDeletedCycles`; reload on toggle; render deleted rows with `opacity-60`; show Restore button when row has `deletedAt`; call `restoreCycle` on click + toast. Satisfies REQ-TOGGLE-1 + REQ-RESTORE-1.
- [ ] PR2.13 Modify `frontend/src/features/budget-structure/views/CycleDetailView.vue` — same pattern for Periods (`showDeletedPeriods`); on Restore click for deleted Period: first click shows inline warning "This will also restore N budget lines." (fetch/display BudgetLine count); second click calls `restorePeriod`; toast on success. Satisfies REQ-TOGGLE-1 + REQ-RESTORE-PERIOD-1.
- [ ] PR2.14 Modify `frontend/src/features/budget-structure/views/CategoryTreeView.vue` — add show-deleted toggle; deleted groups/categories get `opacity-60` + Restore button; call `restoreCategoryGroup` / `restoreCategory` + toast. Satisfies REQ-TOGGLE-1 + REQ-RESTORE-1.
- [ ] PR2.15 Modify `frontend/src/features/budget-structure/views/BudgetLinesView.vue` — add show-deleted toggle; deleted lines get `opacity-60` + Restore button; call `restoreBudgetLine` + toast. Satisfies REQ-TOGGLE-1 + REQ-RESTORE-1.

### Phase 6: Toast Wiring on Structure Actions

- [ ] PR2.16 Verify delete success toasts are pushed in `CycleListView`, `CycleDetailView`, `CategoryTreeView`, `BudgetLinesView` on each entity's delete confirmation success path. Add missing `toastStore.push` calls. Satisfies REQ-TOAST-ACTION-1.

---

## PR 3 — ExecutionRecord Confirm + Execution Toasts (~80 lines)

> Depends on: PR 1 merged to main. Base branch: main (after PR 1 merge). Can be worked in parallel with PR 2.

### Phase 1: Two-Step Delete Confirmation

- [ ] PR3.1 Modify `frontend/src/features/budget-execution/components/ExecutionRecordRow.vue` — add `confirmingDelete` local ref (default `false`); first Delete click sets `confirmingDelete = true` (no API call); second click fires `handleDelete` + resets to `false`; show Cancel button in confirm state that resets to `false`. Satisfies REQ-EXEC-CONFIRM-1.
- [ ] PR3.2 Write component test `frontend/src/features/budget-execution/components/__tests__/ExecutionRecordRow.spec.ts` — first click → confirm state (no API), second click → API call, cancel → reset, row-local isolation. Satisfies REQ-EXEC-CONFIRM-1 scenarios.

### Phase 2: Execution Toast Wiring

- [ ] PR3.3 Modify `ExecutionRecordRow.vue` delete success handler — call `toastStore.push('success', t('budgetExecution.record.deleteSuccess'))`. Satisfies REQ-EXEC-TOAST-1.
- [ ] PR3.4 Modify `ExecutionRecordRow.vue` restore success handler — call `toastStore.push('success', t('budgetExecution.record.restoreSuccess'))`. Satisfies REQ-EXEC-TOAST-1.

### Phase 3: Post-Implementation Review Checkpoint

- [ ] PR3.5 After PR 3 implementation: review two-step UX in context — confirm or document decision to keep two-step vs. upgrade to nested `<dialog>` (product decision #4). Log outcome as a comment in PR 3 description. No code change required unless the decision flips.
