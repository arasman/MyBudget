# Proposal: Budget Execution UI E2E Test Debt

## Intent

The budget-execution feature has 6 E2E spec files, all using `{ request }` (API-only fixture). None exercise the browser UI. The soft-delete-ux change added two-step confirm-delete, restore, and toast feedback to `ExecutionRecordRow`; `ExecutionRecordForm` added create/update flows with OperationDate defaulting to today and currency/exchange-rate selection. Zero UI-level E2E tests cover any of these flows.

Additionally, create and update operations do not fire success toasts today (`ExecutionRecordForm.vue` emits no `toastStore.push()` calls). The existing i18n namespace has `deleteSuccess` and `restoreSuccess` but is missing `createSuccess` and `updateSuccess`. This change adds the missing toast calls, adds the i18n keys, and writes UI-level E2E coverage alongside the existing API-only specs.

## Scope

### In Scope

- **Toast audit + fix**: add `toastStore.push()` calls for create and update in `ExecutionRecordForm.vue`. Add `createSuccess` and `updateSuccess` i18n keys (en + es) following the same pattern as `deleteSuccess`/`restoreSuccess`.
- **Shared auth helper**: extract `loginWithToken(page, token, budgetId)` from `budget-matrix/helpers.ts` into a new `e2e/helpers/auth.ts`. The budget-matrix helper keeps a local re-export (or call-through) so existing tests are not broken. The budget-execution UI specs import from the shared helper.
- **New UI spec file — `execution-ui-crud.spec.ts`** (`{ page, request }`): covers create, update, and OperationDate default; covers currency selection and exchange-rate field display.
- **New UI spec file — `execution-ui-delete-restore.spec.ts`** (`{ page, request }`): covers two-step confirm-delete flow (enter confirm state → cancel resets → confirm deletes with toast); covers restore (deleted record gets Restore button → click → toast); covers restore in a closed period (restore button still present and functional).
- **New UI spec file — `execution-ui-toast.spec.ts`** (`{ page, request }`): explicit toast assertions for create, update, delete, restore.
- **Existing API-only specs**: left untouched (the 11 `{ request }` tests stay as-is).

### Out of Scope

- CSS/opacity assertions for deleted records (covered by unit tests in `ExecutionRecordRow.spec.ts`)
- Include-deleted toggle E2E (modal toggle state is matrix-scoped; covered separately)
- Pagination E2E
- RBAC UI-level tests (API-only RBAC spec already covers the boundary; no new UI RBAC test)
- Budget-matrix E2E (separate feature, separate spec files)

## Capabilities

### New Capabilities

- None

### Modified Capabilities

- `budget-execution-ui`: no spec-level additions; implementation fix for missing `createSuccess`/`updateSuccess` toasts in `ExecutionRecordForm`
- `ephemeral-toast`: no spec changes; two new i18n keys in the `budgetExecution.record` namespace

## Approach

1. **Audit toast firing** in `ExecutionRecordForm.vue` (confirmed: no `toastStore` import today). Import `useToastStore` and push `createSuccess`/`updateSuccess` after successful submit, mirroring `ExecutionRecordRow.vue`.
2. **Add i18n keys** `budgetExecution.record.createSuccess` and `budgetExecution.record.updateSuccess` to `en.json` and `es.json`.
3. **Create `e2e/helpers/auth.ts`** with `loginWithToken(page, token, budgetId)`. Update `budget-matrix/helpers.ts` to re-export from the shared path (or keep its own copy and add a deprecation comment — one file touched).
4. **Write three new UI spec files** under `e2e/budget-execution/`. Each file seeds data via API using the existing `seedBudgetContext` / `createExecution` helpers, then navigates the browser to the matrix view and interacts with `ExecutionListModal` / `ExecutionRecordRow`.
5. **Navigation path**: `/budgets/{budgetId}/cycles/{cycleId}/matrix` → click the MatrixCell amount to open `ExecutionListModal` (`[data-testid="execution-list-modal"]`).

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/e2e/helpers/auth.ts` | New | Shared `loginWithToken` helper |
| `frontend/e2e/budget-matrix/helpers.ts` | Modified | Re-export `loginWithToken` from shared path |
| `frontend/e2e/budget-execution/execution-ui-crud.spec.ts` | New | UI E2E: create, update, OperationDate default, currency selection |
| `frontend/e2e/budget-execution/execution-ui-delete-restore.spec.ts` | New | UI E2E: two-step delete, cancel, restore, restore-in-closed-period |
| `frontend/e2e/budget-execution/execution-ui-toast.spec.ts` | New | UI E2E: explicit toast assertions for all 4 operations |
| `frontend/src/features/budget-execution/components/ExecutionRecordForm.vue` | Modified | Add `toastStore.push()` for create + update |
| `frontend/src/i18n/locales/en.json` | Modified | Add `budgetExecution.record.createSuccess`, `updateSuccess` |
| `frontend/src/i18n/locales/es.json` | Modified | Same keys in Spanish |

## User Decisions (recorded)

| # | Question | Decision |
|---|----------|----------|
| 1 | Toast i18n key naming | Separate `createSuccess` + `updateSuccess` keys (same pattern as `deleteSuccess`/`restoreSuccess`) |
| 2 | Existing API-only specs | Leave 11 `{ request }` specs untouched; add new `{ page, request }` UI specs alongside |
| 3 | Restore in closed period | Include E2E test for this edge case in this change |
| 4 | `loginWithToken` helper | Shared helper in `e2e/helpers/auth.ts`; extracted from budget-matrix; each spec file stays domain-independent |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| MatrixCell click selector ambiguous (multiple cells) | Med | Seed a single budget line so only one cell exists; use `first()` as fallback |
| Toast timing flakiness | Low | Use `getByRole('alert').filter({ hasText: ... }).first()` with 8s timeout, matching budget-structure pattern |
| `loginWithToken` not setting all required localStorage keys | Low | Verify against `budget-matrix/helpers.ts` source (confirmed keys: `accessToken`, `activeBudgetId`) |
| Restore-in-closed-period may need `canWrite` flag on admin user | Med | Seed owner account (auto-admin); verify `v-else-if` restore branch in `ExecutionRecordRow.vue` renders |

## Rollback Plan

Revert the commit(s). The 3 new spec files are additive; the `ExecutionRecordForm.vue` toast addition is a 5-line change. No backend or migration changes. Existing API-only E2E tests are untouched.

## Dependencies

- Soft-delete-ux fully merged (confirmed)
- Toast infrastructure (`useToastStore`, `AppToast`) in place (confirmed)
- `budget-structure-ui-e2e-debt` archived (confirmed: 2026-07-17)
- `budget-matrix/helpers.ts` `loginWithToken` exists and is the extraction source (confirmed)

## Success Criteria

- [ ] `ExecutionRecordForm` fires `createSuccess` toast on create and `updateSuccess` toast on update
- [ ] `budgetExecution.record.createSuccess` and `updateSuccess` keys present in both `en.json` and `es.json`
- [ ] `e2e/helpers/auth.ts` created; `budget-matrix/helpers.ts` updated to use shared helper
- [ ] `execution-ui-crud.spec.ts` passes: create → entry visible; update → entry reflects change; OperationDate defaults to today
- [ ] `execution-ui-delete-restore.spec.ts` passes: confirm-delete flow; cancel resets; restore; restore-in-closed-period
- [ ] `execution-ui-toast.spec.ts` passes: all 4 toast messages verified
- [ ] All 11 existing API-only E2E tests still pass unmodified
- [ ] All new and modified E2E tests pass in CI
