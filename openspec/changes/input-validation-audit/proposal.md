# Proposal: Input Validation Audit

## Intent

Six forms and their backend handlers have inconsistent, incomplete, or silently-failing validation. Users see no feedback on uniqueness violations, amounts can be zero, soft-deleted name collisions cause unhandled 500s, and `store._wrap()` swallows API errors. This audit closes every validation gap across all budget entities so every invalid input produces a clear, localized error message.

## Scope

### In Scope

- **Track A (Frontend)**: Fix `_wrap()` to re-throw; add try/catch + error toasts in all view action handlers; add missing inline validation to 6 forms + CycleListView inline edit; add ~28 i18n keys (en + es); migrate ExecutionRecordForm inline banner to toastStore; fix hardcoded English strings in CategoryGroupForm/CategoryForm
- **Track B (Backend)**: Fix CategoryGroup/Category `IgnoreQueryFilters` for uniqueness; add Budget/Cycle/Period/BudgetLine name uniqueness checks (soft-delete aware); add operationDate period-range check; fix BudgetLine amount `> 0`; align Execution note to always-required; add `HasMaxLength` for BudgetLineRevision.Note

### Out of Scope

- New DB unique indexes (only CategoryGroup and Category have them today; others rely on handler-level checks)
- Period overlap with soft-deleted siblings (overlap checks remain active-only)
- Frontend-side uniqueness pre-checks (uniqueness stays server-enforced, surfaced via error toasts)
- Refactoring form architecture or store patterns beyond the `_wrap()` fix

## Capabilities

### New Capabilities

None

### Modified Capabilities

- `budget-structure`: Add name uniqueness checks (Budget, CategoryGroup, Category, Cycle, Period, BudgetLine); fix `IgnoreQueryFilters` for soft-delete aware uniqueness; fix amount validator `> 0`; add `HasMaxLength` on BudgetLineRevision.Note
- `budget-structure-ui`: Add inline validation to all 6 forms + CycleListView; fix `_wrap()` re-throw; add error toasts; add i18n keys; fix hardcoded English strings
- `budget-execution`: Add operationDate period-range check (backend); align note to always-required; migrate inline error banner to toast; add decimal-place validation

## Approach

Two parallel tracks. Track B (backend) adds missing validators, uniqueness checks with `IgnoreQueryFilters`, and new error codes. Track A (frontend) fixes the store error-swallow pattern, adds inline validation to all forms, wires error toasts for business-rule violations, and adds i18n keys. Error surfacing contract: local constraints inline at input, business rules and API errors via `toastStore.push({ type: 'error' })`.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/src/features/budget-structure/store.ts` | Modified | `_wrap()` re-throws errors |
| `frontend/.../components/{6 forms}` | Modified | Add inline validation, maxlength, i18n |
| `frontend/.../views/CycleListView.vue` | Modified | Add validation to inline edit |
| `frontend/.../views/{CategoryTree,CycleDetail,BudgetLines}View.vue` | Modified | Add try/catch + error toasts |
| `frontend/src/i18n/locales/{en,es}.json` | Modified | ~28 new validation keys |
| `frontend/.../ExecutionRecordForm.vue` | Modified | Migrate inline banner to toast |
| `backend/.../Create+UpdateCategoryGroup/Handler.cs` | Modified | `IgnoreQueryFilters()` |
| `backend/.../Create+UpdateCategory/Handler.cs` | Modified | `IgnoreQueryFilters()` |
| `backend/.../Create+RenameBudget/Handler.cs` | Modified | Add name uniqueness |
| `backend/.../Create+UpdatePeriod/Handler.cs` | Modified | Add name uniqueness |
| `backend/.../Create+UpdateBudgetLine/Handler+Validator.cs` | Modified | Add name uniqueness, fix amount > 0 |
| `backend/.../Create+UpdateExecutionRecord/Handler+Validator.cs` | Modified | Add operationDate range, note always required |
| `backend/.../BudgetLineRevisionConfiguration.cs` | Modified | Add `HasMaxLength` for Note |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Existing soft-deleted rows block re-creation of same-named entities | Med | Handler returns clear DUPLICATE error code; user must permanently delete or rename |
| `_wrap()` re-throw breaks callers that do not catch | Low | Audit every `_wrap()` call site during implementation |
| 400-line PR budget exceeded | High | Split into Track A (frontend) and Track B (backend) PRs |

## Rollback Plan

Revert the feature branch. No migrations, no schema changes, no data transformations. All changes are additive validation logic and i18n keys.

## Dependencies

- `ephemeral-toast` spec (already implemented and archived)

## Success Criteria

- [ ] Every field in the validation spec table has both frontend inline validation and backend enforcement
- [ ] Uniqueness checks include soft-deleted rows via `IgnoreQueryFilters`
- [ ] All API error codes produce localized error toasts (no silent failures)
- [ ] No hardcoded English validation strings remain
- [ ] BudgetLine amount rejects 0
- [ ] ExecutionRecord.note required for all entry types
- [ ] CycleListView inline edit validates name/dates
- [ ] All 28 i18n keys present in both en.json and es.json
