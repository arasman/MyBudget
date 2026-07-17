# Proposal: Budget Structure UI E2E Test Debt

## Intent

The soft-delete-ux feature (merged in 4 PRs ending at 98de217) added show-deleted toggle, restore actions, cascade disclosure, and toast feedback across all budget-structure entities. Zero E2E tests cover these flows. Additionally, existing CRUD tests (create, edit, set-active, delete) lack toast assertions entirely. Some CRUD operations may not even fire toasts in the frontend implementation, violating REQ-TOAST-ACTION-1. This change closes the E2E gap and fixes any missing toast implementations.

## Scope

### In Scope
- **Toast audit**: verify which CRUD operations (create, edit/rename, set-active, delete) actually fire toasts in the frontend for each entity (Cycle, Period, CategoryGroup, Category, BudgetLine). Fix any missing implementations.
- **Toggle E2E**: test toggle ON (deleted items appear) AND toggle OFF (deleted items disappear) for all 4 entity domains.
- **Restore E2E**: test restore action for Cycle, CategoryGroup, Category, BudgetLine; test Period restore with cascade disclosure confirm and cancel paths.
- **Toast E2E**: verify toast appears AND text matches expected i18n resolution (e.g., "Cycle deleted successfully") for all operations.
- **Retrofit toast assertions**: add toast appearance + text checks into existing CRUD happy-path tests.
- **Seed helpers**: extend `helpers.ts` with functions that create-then-soft-delete entities via API for toggle/restore test setup.

### Out of Scope
- CSS class assertions for visual distinction of deleted items
- Session-scoped toggle persistence across navigation (fragile, low value for E2E)
- Multi-budget context tests
- Error-path toast suppression (REQ-TOAST-ACTION-1 "no toast on API error")
- Budget-execution E2E debt (separate change)

## Capabilities

### New Capabilities
- None

### Modified Capabilities
- `budget-structure-ui`: no spec-level changes; E2E coverage of existing REQ-TOGGLE-1, REQ-RESTORE-1, REQ-RESTORE-PERIOD-1, REQ-TOAST-ACTION-1
- `ephemeral-toast`: no spec-level changes; potential frontend implementation fix if toast is missing for some CRUD ops

## Approach

1. **Audit toast firing** in frontend composables/views for each entity CRUD operation. Compare against REQ-TOAST-ACTION-1 i18n key table. Fix gaps by adding `toastStore.push()` calls where missing.
2. **Extend `helpers.ts`** with `seedDeletedCycle()`, `seedDeletedPeriod()`, `seedDeletedCategoryGroup()`, `seedDeletedCategory()`, `seedDeletedBudgetLine()` using API calls (create then DELETE).
3. **Add `test.describe('soft-delete / restore')` blocks** to each of the 4 existing entity spec files (Option A from exploration).
4. **Retrofit toast assertions** into existing CRUD `test()` blocks — after create/delete actions, assert `page.getByRole('alert')` is visible with expected i18n text.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/e2e/budget-structure/helpers.ts` | Modified | Add seed-deleted-entity helpers |
| `frontend/e2e/budget-structure/budget-structure-cycles.spec.ts` | Modified | Add soft-delete/restore describe block + retrofit toast |
| `frontend/e2e/budget-structure/budget-structure-periods.spec.ts` | Modified | Add soft-delete/restore with cascade + retrofit toast |
| `frontend/e2e/budget-structure/budget-structure-categories.spec.ts` | Modified | Add soft-delete/restore for group+category + retrofit toast |
| `frontend/e2e/budget-structure/budget-structure-lines.spec.ts` | Modified | Add soft-delete/restore + retrofit toast |
| `frontend/src/` (composables/views) | Modified | Fix missing toast calls if audit finds gaps |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Toast not fired for some CRUD ops (edit, set-active) | High | Audit first; fix before writing assertions |
| Toggle/restore selectors unknown until audit | Med | Verify aria-labels/test-ids during spec phase |
| Test flakiness from toast auto-dismiss timing | Low | Use `waitFor` with short timeout; toasts last 3s |

## Rollback Plan

Revert the commit(s). All changes are E2E tests and minor frontend toast fixes — no backend or data changes. Existing tests remain unaffected if reverted.

## Dependencies

- Soft-delete-ux feature fully merged (confirmed: 98de217)
- Toast infrastructure (useToastStore, AppToast) in place (confirmed: ephemeral-toast spec)

## Success Criteria

- [ ] All CRUD operations for all 5 entity types fire success toasts (audit + fix complete)
- [ ] Toggle ON/OFF E2E tests pass for Cycles, Periods, CategoryGroups, Categories, BudgetLines
- [ ] Restore E2E tests pass for all entities including Period cascade disclosure
- [ ] Existing CRUD tests include toast appearance + text assertions
- [ ] All new and modified E2E tests pass in CI
