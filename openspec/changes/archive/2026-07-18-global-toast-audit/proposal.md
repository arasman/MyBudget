# Proposal: Global Toast Audit

## Intent

13 mutation operations across BudgetMatrix and BudgetSelection views silently succeed without user feedback. One component (`ChangePasswordModal`) uses `notificationStore` instead of `toastStore`, violating the established pattern. This change closes all toast gaps and aligns the inconsistency.

## Scope

### In Scope
- Wire orphaned `budgetStructure.selection.createSuccess` key in `BudgetSelectionView.vue` (post-navigation toast)
- Add `renameSuccess` toast to budget rename handler in `BudgetSelectionView.vue`
- Add toasts to 3 inline-add operations in `BudgetMatrixView.vue` (group, category, line)
- Add toasts to `MatrixGroupRow.vue` (saveEdit, doDelete, doRestore)
- Add toasts to `MatrixCategoryRow.vue` (saveEdit, doDelete, doRestore)
- Add toasts to `MatrixLineRow.vue` (doDelete, doRestore)
- Migrate `ChangePasswordModal.vue` from `notificationStore` to `toastStore`
- Add ~8 new i18n keys to both `en.json` and `es.json`

### Out of Scope
- `InviteUserModal.vue` inline `successMessage` (adequate UX, low priority)
- Error toast behavior (error handling unchanged)
- Toast store shape or autoDismiss behavior
- New E2E tests (existing patterns cover; debt tracked separately)

## Capabilities

### New Capabilities
None

### Modified Capabilities
- `ephemeral-toast`: expanding coverage to matrix rows and budget selection operations; no spec-level behavior change (same `push()` contract)

## Approach

Add `useToastStore()` injection + `push({ type: 'success', title: t('<key>') })` calls to each missing operation, following the established call pattern. All calls use `type: 'success'`, `title` only, no `message` or custom `autoDismiss`. For `BudgetSelectionView.onBudgetCreated`, the toast fires after `router.push()` — the store survives navigation. Migrate `ChangePasswordModal` by replacing `notificationStore` with `toastStore` (2-line change).

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/src/views/BudgetSelectionView.vue` | Modified | Wire createSuccess toast, add renameSuccess toast |
| `frontend/src/views/BudgetMatrixView.vue` | Modified | Inject toastStore, add toasts to 3 add operations |
| `frontend/src/components/matrix/MatrixGroupRow.vue` | Modified | Inject toastStore, add 3 toasts |
| `frontend/src/components/matrix/MatrixCategoryRow.vue` | Modified | Inject toastStore, add 3 toasts |
| `frontend/src/components/matrix/MatrixLineRow.vue` | Modified | Inject toastStore, add 2 toasts |
| `frontend/src/components/ChangePasswordModal.vue` | Modified | notificationStore -> toastStore migration |
| `frontend/src/i18n/locales/en.json` | Modified | ~8 new keys |
| `frontend/src/i18n/locales/es.json` | Modified | ~8 new keys |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| i18n key mismatch between locales | Low | Add keys to both files in same commit |
| Post-navigation toast lost | Low | Toast store is a Pinia singleton, survives router push (verified pattern) |

## Rollback Plan

Revert the single feature branch commit. No data model, API, or store shape changes — pure additive UI feedback.

## Dependencies

None. All infrastructure (toastStore, AppToast component, i18n setup) already exists.

## Success Criteria

- [ ] All 13 previously-silent mutations show a success toast
- [ ] `ChangePasswordModal` uses `toastStore` instead of `notificationStore`
- [ ] No i18n missing-key warnings in either locale
- [ ] Existing toast behavior unchanged (autoDismiss, stacking, bell exclusion)
