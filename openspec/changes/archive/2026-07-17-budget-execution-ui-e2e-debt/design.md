# Design: Budget Execution UI E2E Test Debt

## Technical Approach

Add missing `toastStore.push()` calls to `ExecutionRecordForm.vue` (create/update), add corresponding i18n keys, extract a shared `loginWithToken` auth helper, and write 3 new UI E2E spec files under `e2e/budget-execution/`. All new specs use `{ page, request }` fixtures, reuse `seedBudgetMatrixFixture` from `budget-matrix/helpers.ts` for data setup, and follow the navigation pattern established in `budget-matrix/execution-crud.spec.ts` (dblclick on `matrix-cell-ejecutado` to open modal).

## Architecture Decisions

| # | Decision | Choice | Rejected | Rationale |
|---|----------|--------|----------|-----------|
| 1 | Shared auth helper location | `e2e/helpers/auth.ts` with `loginWithToken(page, { accessToken, refreshToken, activeBudgetId })` | Keep duplicate in each domain folder | budget-structure already sets `refreshToken` in localStorage; budget-matrix does not. Shared helper normalizes both. budget-matrix re-exports from shared. |
| 2 | `loginWithToken` signature | Object param `{ accessToken, refreshToken?, activeBudgetId? }` — refreshToken optional with default to empty string | Positional args `(page, token, budgetId)` matching current budget-matrix | Object shape is self-documenting, forward-compatible, and handles the refreshToken gap without breaking callers. |
| 3 | Fixture setup for UI specs | Reuse `seedBudgetMatrixFixture` from `budget-matrix/helpers.ts` | Create new `seedBudgetExecutionUIFixture` | The matrix fixture already creates user + budget + cycle + periods + groups + categories + lines + token. No duplication needed. |
| 4 | Closed-period fixture | Reuse existing `closePeriodApi` from `budget-matrix/helpers.ts` (or `closePeriod` from `budget-execution/helpers.ts`) | Create new helper | Both already exist and work identically (PATCH period status). |
| 5 | `expectToast` helper | Import from shared `e2e/helpers/toast.ts` (extract from `budget-structure/helpers.ts`) | Copy into each spec file | Single source of truth; same 8s timeout pattern used everywhere. |
| 6 | Restore on closed period — error handling | `ExecutionRecordRow.handleRestore` has empty `catch {}` — store throws, toast does NOT fire, no error toast shown. Design: accept current behavior (silent fail) for this change; E2E test asserts restore SUCCESS (API allows restore on closed period for owner). | Add error toast on catch | The backend allows restore on closed periods for owners (`canWrite` is true). The 409 PERIOD_CLOSED guard only blocks create/update/delete, not restore. The `v-else-if` block in the template confirms restore is rendered even when `periodClosed` is true. |
| 7 | Toast placement in ExecutionRecordForm | After `emit('saved')` and before form reset (create) / at end of try block (update) | Inside ExecutionListModal's `onFormSaved` callback | Mirrors `ExecutionRecordRow` pattern where the component performing the action fires its own toast. The modal should not know about toast semantics. |

## Data Flow

```
User dblclick MatrixCell
    |
    v
matrixStore.openExecutionModal(lineId, periodId)
    |
    v
ExecutionListModal renders (list mode)
    |
    +---> ExecutionRecordForm (create)
    |         |
    |         v  handleSubmit() success
    |         +-> matrixStore.createExecution()
    |         +-> toastStore.push({ type: 'success', title: t('...createSuccess') })  [NEW]
    |         +-> emit('saved')
    |
    +---> ExecutionRecordRow (per record)
              |
              +-> edit click -> ExecutionListModal switches to edit mode
              |     +-> ExecutionRecordForm (edit)
              |           +-> matrixStore.updateExecution()
              |           +-> toastStore.push({ type: 'success', title: t('...updateSuccess') })  [NEW]
              |           +-> emit('saved')
              |
              +-> delete flow -> toastStore.push('deleteSuccess')  [EXISTING]
              +-> restore flow -> toastStore.push('restoreSuccess') [EXISTING]
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/features/budget-execution/components/ExecutionRecordForm.vue` | Modify | Import `useToastStore`, call `toastStore.push()` after successful create/update, use `isEdit` flag to select i18n key |
| `src/i18n/locales/en.json` | Modify | Add `budgetExecution.record.createSuccess` ("Entry created successfully") and `updateSuccess` ("Entry updated successfully") |
| `src/i18n/locales/es.json` | Modify | Add `budgetExecution.record.createSuccess` ("Entrada creada exitosamente") and `updateSuccess` ("Entrada actualizada exitosamente") |
| `e2e/helpers/auth.ts` | Create | Shared `loginWithToken(page, opts)` setting `accessToken`, `refreshToken`, `activeBudgetId` in localStorage |
| `e2e/helpers/toast.ts` | Create | Shared `expectToast(page, text)` extracted from `budget-structure/helpers.ts` |
| `e2e/budget-matrix/helpers.ts` | Modify | Replace inline `loginWithToken` with re-export from `e2e/helpers/auth.ts`; keep same call signature via adapter or update call sites |
| `e2e/budget-structure/helpers.ts` | Modify | Replace inline `expectToast` with re-export from `e2e/helpers/toast.ts` |
| `e2e/budget-execution/execution-ui-crud.spec.ts` | Create | UI E2E: create record via form, verify row appears; edit record via edit button, verify change; OperationDate defaults to today |
| `e2e/budget-execution/execution-ui-delete-restore.spec.ts` | Create | UI E2E: two-step delete confirm, cancel resets, confirm deletes; toggle include-deleted, restore record; restore on closed period |
| `e2e/budget-execution/execution-ui-toast.spec.ts` | Create | UI E2E: assert toast messages for create, update, delete, restore |

## Interfaces / Contracts

```typescript
// e2e/helpers/auth.ts
export interface LoginTokens {
  accessToken: string
  refreshToken?: string
  activeBudgetId?: string
}

export async function loginWithToken(
  page: Page,
  tokens: LoginTokens,
): Promise<void>

// e2e/helpers/toast.ts
export async function expectToast(page: Page, text: string): Promise<void>
```

```typescript
// ExecutionRecordForm.vue — new toast call (inserted after emit('saved') in try block)
// Create path:
toastStore.push({ type: 'success', title: t('budgetExecution.record.createSuccess') })

// Update path:
toastStore.push({ type: 'success', title: t('budgetExecution.record.updateSuccess') })
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| E2E (UI) | Create record via modal form, verify row appears + toast | `execution-ui-crud.spec.ts` — seed via API, navigate browser, interact with form |
| E2E (UI) | Edit record via edit button, verify update + toast | Same spec — click Edit on row, modify, submit |
| E2E (UI) | Two-step delete confirm + cancel + delete + toast | `execution-ui-delete-restore.spec.ts` |
| E2E (UI) | Restore via include-deleted toggle + Restore button + toast | Same spec |
| E2E (UI) | Restore on closed period (owner can restore) | Same spec — seed + close period, then restore |
| E2E (UI) | All 4 toast messages visible with correct text | `execution-ui-toast.spec.ts` |

## Migration / Rollout

No migration required. All changes are additive (new spec files, new i18n keys, one 5-line production patch). Existing 11 API-only specs remain untouched.

## Open Questions

- [x] i18n keys `createSuccess`/`updateSuccess` do NOT exist in `budgetExecution.record` namespace — confirmed, must be added (en.json line 342, es.json line 342)
- [x] `loginWithToken` in budget-matrix sets `accessToken` + `activeBudgetId` but NOT `refreshToken` — confirmed gap; budget-structure sets both. Shared helper normalizes.
- [x] Restore on closed period: API allows it (no 409), UI renders restore button via `v-else-if` block (ExecutionRecordRow line 96-108). E2E should assert success path.
