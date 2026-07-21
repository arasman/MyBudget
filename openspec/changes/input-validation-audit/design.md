# Design: Input Validation Audit

## Technical Approach

Two-track, parallel implementation. Track B (backend) adds missing uniqueness checks with `IgnoreQueryFilters()`, new error codes, and validator fixes. Track A (frontend) fixes the store error-swallow pattern, adds inline validation to all forms, wires error toasts for business-rule violations, and adds i18n keys. The two tracks share a contract: backend returns `{ error: "SCREAMING_SNAKE_CODE" }` at 422; frontend maps known codes to i18n keys via a utility function.

## Architecture Decisions

### Decision: `_wrap()` re-throw strategy

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Always re-throw after setting `store.error` | Callers must catch; consistent behavior | **Chosen** |
| Re-throw only non-400 errors | Callers get silent 400s; inconsistent | Rejected |
| Return `Result<T>` from `_wrap` | Large refactor; every call site changes signature | Rejected |

**Rationale**: `_wrap()` currently runs `fn().finally(() => loading.value = false)` -- it does not catch at all; errors already propagate as unhandled rejections. The fix adds `.catch(e => { error.value = extractMessage(e); throw e })` before `.finally()`, making the re-throw explicit. Callers already `await` each store action; adding `try/catch` at the view level is mechanical.

### Decision: Error code extraction utility

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Shared `extractApiErrorCode(err): string | null` helper | Reusable, single point of change | **Chosen** |
| Inline `(err as any).response?.data?.error` in each catch | Duplicated, fragile | Rejected |

**Rationale**: Backend endpoints return `{ error: "CODE" }` for business rule violations and `{ detail: "CODE" }` for ProblemDetails. A single utility (`features/budget-structure/utils/apiError.ts`) normalizes both shapes and returns the code string or null.

### Decision: Decimal precision validation approach

| Option | Tradeoff | Decision |
|--------|----------|----------|
| JS regex `/^\d+(\.\d{1,N})?$/` in `validate()` | Precise, framework-independent | **Chosen** |
| HTML `step` attribute only | UX hint only; no enforcement | Rejected — keep for UX, not validation |

**Rationale**: `step="0.01"` gives browser UX hints but does not prevent programmatic submission of invalid values. The `validate()` function uses a regex check; the `step` attribute remains for UX.

### Decision: Frontend operationDate period-range check

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Best-effort frontend check when period dates are in props | Fast feedback; may be stale | **Chosen** |
| Backend-only | No stale data risk; slower feedback | Backend always authoritative regardless |

**Rationale**: ExecutionRecordForm already receives period context. Adding a local date-range check is trivial and gives instant feedback. Backend remains the authoritative validator.

### Decision: Soft-delete uniqueness -- handler-level vs DB unique filtered index

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Handler-level `IgnoreQueryFilters()` check | Works with current EF setup; no migration | **Chosen** |
| Add SQL filtered unique indexes | Better DB integrity; requires migration | Rejected for this audit scope |

**Rationale**: CategoryGroup and Category already have unfiltered DB unique indexes (which is what causes the 500 today). For Budget, Cycle, Period, BudgetLine -- no DB indexes exist; adding them is out of scope per proposal. Handler-level checks with `IgnoreQueryFilters()` close the gap without migrations.

## Data Flow

### Error surfacing flow (after changes)

```
  User submits form
       │
       ▼
  Form.validate() ─── fails ──► inline field error (i18n)
       │ passes
       ▼
  View calls store.action()
       │
       ▼
  store._wrap(fn)
       │
    fn() calls API
       │
       ├── 2xx ──► store updates state ──► success toast
       │
       └── 4xx/5xx ──► _wrap sets store.error, RE-THROWS
                              │
                              ▼
                     View catch block
                              │
                     extractApiErrorCode(err)
                              │
                  ┌───── known code ──► toastStore.push({ type:'error',
                  │                      title: t('entity.errors.<code>') })
                  │
                  └───── unknown ────► toastStore.push({ type:'error',
                                        title: t('common.error') })
```

### Backend uniqueness check flow

```
  Endpoint receives request
       │
       ▼
  FluentValidation (format, required, ranges)
       │ passes
       ▼
  Handler: db.Entity
    .IgnoreQueryFilters()
    .AnyAsync(name == normalizedName && scope == parentId)
       │
       ├── duplicate ──► Result.Failure("ENTITY_NAME_DUPLICATE")
       │                    ──► Endpoint returns 422 { error: "..." }
       │
       └── unique ──► SaveChangesAsync ──► 201/200
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `frontend/.../budget-structure/store.ts` | Modify | Add `.catch(e => { error.value = msg; throw e })` to `_wrap()` |
| `frontend/.../budget-structure/utils/apiError.ts` | Create | `extractApiErrorCode(err): string \| null` utility |
| `frontend/.../components/CategoryGroupForm.vue` | Modify | Add maxlength, i18n validation messages |
| `frontend/.../components/CategoryForm.vue` | Modify | Add maxlength, i18n validation messages |
| `frontend/.../components/CycleForm.vue` | Modify | Add name maxlength validation |
| `frontend/.../components/PeriodForm.vue` | Modify | Add name maxlength validation |
| `frontend/.../components/BudgetLineModal.vue` | Modify | Add `validate()`, amount>0, name maxlength |
| `frontend/.../components/CreateBudgetModal.vue` | Modify | Add duplicate error code mapping, toast |
| `frontend/.../components/ExecutionRecordForm.vue` | Modify | Add decimal validation, date range, migrate to toast |
| `frontend/.../views/CategoryTreeView.vue` | Modify | Add try/catch + error toast on store calls |
| `frontend/.../views/CycleListView.vue` | Modify | Add inline edit validation + error toast |
| `frontend/.../views/CycleDetailView.vue` | Modify | Add try/catch + error toast on period store calls |
| `frontend/.../views/BudgetLinesView.vue` | Modify | Add try/catch + error toast on line store calls |
| `frontend/src/i18n/locales/en.json` | Modify | Add ~28 validation/error keys |
| `frontend/src/i18n/locales/es.json` | Modify | Add ~28 validation/error keys |
| `backend/.../CreateCategoryGroup/Handler.cs` | Modify | Add `IgnoreQueryFilters()` to uniqueness check |
| `backend/.../UpdateCategoryGroup/Handler.cs` | Modify | Add `IgnoreQueryFilters()` to uniqueness check |
| `backend/.../CreateCategory/Handler.cs` | Modify | Add `IgnoreQueryFilters()` to uniqueness check |
| `backend/.../UpdateCategory/Handler.cs` | Modify | Add `IgnoreQueryFilters()` to uniqueness check |
| `backend/.../CreateBudget/Handler.cs` | Modify | Add name uniqueness check (per user, soft-delete aware) |
| `backend/.../RenameBudget/Handler.cs` | Modify | Add name uniqueness check |
| `backend/.../CreateCycleHandler.cs` | Modify | Add name uniqueness check (per budget) |
| `backend/.../CreatePeriod/Handler.cs` | Modify | Add name uniqueness check (per cycle) |
| `backend/.../UpdatePeriod/Handler.cs` | Modify | Add name uniqueness check |
| `backend/.../CreateBudgetLine/Handler.cs` | Modify | Add name uniqueness check (per group+category) |
| `backend/.../UpdateBudgetLine/Handler.cs` | Modify | Add name uniqueness check |
| `backend/.../CreateBudgetLine/Validator.cs` | Modify | Change `GreaterThanOrEqualTo(0)` to `GreaterThan(0)` |
| `backend/.../CreateExecutionRecord/Validator.cs` | Modify | Add note always-required; operationDate range |
| `backend/.../UpdateExecutionRecord/Validator.cs` | Modify | Same as above |
| `backend/.../BudgetLineRevisionConfiguration.cs` | Modify | Add `HasMaxLength(200)` for Note |
| `backend/.../CreateCategoryGroupEndpoint.cs` | Modify | Normalize error response shape if needed |

## Interfaces / Contracts

```typescript
// frontend/.../budget-structure/utils/apiError.ts
export function extractApiErrorCode(err: unknown): string | null {
  const ax = err as { response?: { data?: { error?: string; detail?: string } } }
  return ax.response?.data?.error ?? ax.response?.data?.detail ?? null
}
```

Backend error codes (new, SCREAMING_SNAKE_CASE per convention):
- `BUDGET_NAME_DUPLICATE`
- `CYCLE_NAME_DUPLICATE`
- `PERIOD_NAME_DUPLICATE`
- `BUDGET_LINE_NAME_DUPLICATE`
- `OPERATION_DATE_OUT_OF_RANGE`

Existing codes reused: `CATEGORY_GROUP_NAME_DUPLICATE`, `CATEGORY_NAME_DUPLICATE`.

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit (BE) | Each handler uniqueness check with soft-deleted rows | In-memory EF + seed deleted entity with same name |
| Unit (BE) | Validator rule changes (amount>0, note required, date range) | FluentValidation `TestValidate()` |
| Unit (FE) | `extractApiErrorCode` utility | Vitest: mock AxiosError shapes |
| Unit (FE) | Each form `validate()` function | Vitest: boundary values (empty, 201 chars, 0 amount, 3+ decimals) |
| Integration (FE) | View error toast on API 422 | Component test: mock store to throw, assert toastStore.push called |
| E2E | Not in scope | Existing Playwright suite covers happy paths; validation E2E deferred |

## Migration / Rollout

No migration required. `HasMaxLength(200)` on BudgetLineRevision.Note may generate an EF migration if EF migrations are in use -- this is a column-length constraint, not a data migration. Existing data with notes longer than 200 chars (if any) should be checked before applying.

## Open Questions

- [x] `_wrap()` re-throw: always re-throw -- decided above.
- [x] Decimal validation: JS regex in `validate()`, `step` for UX only -- decided above.
- [x] Frontend operationDate range: best-effort if period dates available -- decided above.
- [ ] BudgetLineRevision.Note `HasMaxLength(200)` -- confirm no existing notes exceed 200 chars before applying the EF migration.
- [ ] Backend error response shape inconsistency: some endpoints return `{ error: "CODE" }`, others use ProblemDetails `{ detail: "CODE" }`. The `extractApiErrorCode` utility handles both, but unifying the shape is out of scope.
