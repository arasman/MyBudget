# Tasks: Input Validation Audit

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | ~500–650 (additions + deletions) |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 (backend) → PR2 (frontend) |
| Delivery strategy | feature-branch-chain |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 — Track B | Backend uniqueness checks, validator fixes, EF config | PR1 → `feat/input-validation-audit` | Self-contained; tested with in-memory EF + FluentValidation |
| 2 — Track A | Frontend store fix, inline validation, error toasts, i18n | PR2 → `feat/input-validation-audit/backend` | Depends on PR1 error codes being defined |

---

## Phase 1: Foundation (both tracks — parallel start)

### Track B — branch `feat/input-validation-audit/backend`

- [ ] B-1.1 `CreateCategoryGroupHandler.cs` — add `IgnoreQueryFilters()` to name uniqueness query. Satisfies REQ-CG-01.
- [ ] B-1.2 `UpdateCategoryGroupHandler.cs` — add `IgnoreQueryFilters()` (excluding self) to name uniqueness query. Satisfies REQ-CG-02.
- [ ] B-1.3 `CreateCategoryHandler.cs` — add `IgnoreQueryFilters()` to name uniqueness query. Satisfies REQ-CAT-01.
- [ ] B-1.4 `UpdateCategoryHandler.cs` — add `IgnoreQueryFilters()` (excluding self) to name uniqueness query. Satisfies REQ-CAT-02.
- [ ] B-1.5 `CreateBudgetHandler.cs` — add `IgnoreQueryFilters()` name+user uniqueness check; return `BUDGET_NAME_DUPLICATE` 422. Satisfies REQ-BUDGET-UNIQUE-1.
- [ ] B-1.6 `RenameBudgetHandler.cs` — add `IgnoreQueryFilters()` name+user uniqueness check (self-excluded); return `BUDGET_NAME_DUPLICATE` 422. Satisfies REQ-BUDGET-UNIQUE-1.
- [ ] B-1.7 `CreateCycleHandler.cs` — add `IgnoreQueryFilters()` name+budgetId uniqueness check; return `CYCLE_NAME_DUPLICATE` 422. Satisfies REQ-CYC-NAME-1.
- [ ] B-1.8 `UpdateCycleHandler.cs` — add `IgnoreQueryFilters()` name+budgetId uniqueness check (self-excluded); return `CYCLE_NAME_DUPLICATE` 422. Satisfies REQ-CYC-NAME-1.
- [ ] B-1.9 `CreatePeriodHandler.cs` — add `IgnoreQueryFilters()` name+cycleId uniqueness; return `PERIOD_NAME_DUPLICATE` 422. Satisfies REQ-PER-NAME-1.
- [ ] B-1.10 `UpdatePeriodHandler.cs` — add `IgnoreQueryFilters()` name+cycleId uniqueness (self-excluded); return `PERIOD_NAME_DUPLICATE` 422. Satisfies REQ-PER-NAME-1.
- [ ] B-1.11 `CreateBudgetLineHandler.cs` — add `IgnoreQueryFilters()` name+(categoryGroupId,categoryId) uniqueness; return `BUDGET_LINE_NAME_DUPLICATE` 422. Satisfies REQ-BL-NAME-1.
- [ ] B-1.12 `UpdateBudgetLineHandler.cs` — add `IgnoreQueryFilters()` name+(categoryGroupId,categoryId) uniqueness (self-excluded); return `BUDGET_LINE_NAME_DUPLICATE` 422. Satisfies REQ-BL-NAME-1.
- [ ] B-1.13 `CreateBudgetLineValidator.cs` — change `GreaterThanOrEqualTo(0)` → `GreaterThan(0)` for Amount. Satisfies REQ-BL-AMOUNT-1.
- [ ] B-1.14 `BudgetLineRevisionConfiguration.cs` — add `.Property(x => x.Note).HasMaxLength(200)`. Add code comment noting existing data must be checked before migration. Satisfies REQ-BL-NOTE-MAX-1.
- [ ] B-1.15 `CreateExecutionRecordValidator.cs` — remove `When(EntryType == CreditNote/DebitNote)` guard on Note rule; make Note always required with error code `NOTE_REQUIRED`. Satisfies REQ-EXEC-4.
- [ ] B-1.16 `UpdateExecutionRecordValidator.cs` — same Note always-required fix; error code `NOTE_REQUIRED`. Satisfies REQ-EXEC-4.
- [ ] B-1.17 `CreateExecutionRecordValidator.cs` — add OperationDate period-range rule: when not null, OperationDate must be >= Period.StartDate and <= Period.EndDate; error code `OPERATION_DATE_OUT_OF_RANGE` 422. Satisfies REQ-EXEC-DATE-RANGE-1.
- [ ] B-1.18 `UpdateExecutionRecordValidator.cs` — add same OperationDate period-range rule. Satisfies REQ-EXEC-DATE-RANGE-1.

### Track A — branch `feat/input-validation-audit/frontend`

- [ ] A-1.1 `features/budget-structure/store.ts` — add `.catch(e => { error.value = extractMessage(e); throw e })` to `_wrap()` before `.finally()`. Satisfies REQ-WRAP-RETHROW-1.
- [ ] A-1.2 `features/budget-structure/utils/apiError.ts` — create `extractApiErrorCode(err: unknown): string | null` normalising `{ error }` and ProblemDetails `{ detail }` shapes. Satisfies REQ-ERROR-TOAST-1 (shared contract).

---

## Phase 2: Core Implementation

### Track B

- [ ] B-2.1 Run backend build (`dotnet build`) and confirm no compile errors across all modified handlers and validators.

### Track A

- [ ] A-2.1 `CategoryGroupForm` — add `validate()`: nameRequired, nameTooLong (max 200). Satisfies REQ-FORM-INLINE-VAL-1.
- [ ] A-2.2 `CategoryGroupForm` view handler — add `try/catch` on store call; `extractApiErrorCode` → `toastStore.push` for `CATEGORY_GROUP_NAME_DUPLICATE` and unknown fallback. Satisfies REQ-ERROR-TOAST-1.
- [ ] A-2.3 `CategoryForm` — add `validate()`: nameRequired, nameTooLong (max 200). Satisfies REQ-FORM-INLINE-VAL-1.
- [ ] A-2.4 `CategoryForm` view handler — add `try/catch`; toast for `CATEGORY_NAME_DUPLICATE`. Satisfies REQ-ERROR-TOAST-1.
- [ ] A-2.5 `CycleForm` — add nameTooLong (max 200) to `validate()`. Satisfies REQ-FORM-INLINE-VAL-1.
- [ ] A-2.6 `CycleForm` view handler — add `try/catch`; toast for `CYCLE_NAME_DUPLICATE`. Satisfies REQ-ERROR-TOAST-1.
- [ ] A-2.7 `PeriodForm` — add `validate()`: nameTooLong, startDateRequired, endDateRequired. Satisfies REQ-FORM-INLINE-VAL-1.
- [ ] A-2.8 `PeriodForm` view handler — add `try/catch`; toast for `PERIOD_NAME_DUPLICATE`. Satisfies REQ-ERROR-TOAST-1.
- [ ] A-2.9 `BudgetLineModal` — add `validate()`: nameRequired, nameTooLong, amountRequired, amountPositive, noteMaxLength (200). Satisfies REQ-FORM-INLINE-VAL-1.
- [ ] A-2.10 `BudgetLineModal` view handler — add `try/catch`; toast for `BUDGET_LINE_NAME_DUPLICATE`. Satisfies REQ-ERROR-TOAST-1.
- [ ] A-2.11 `CreateBudgetModal` view handler — replace generic fallback with `extractApiErrorCode`; toast for `BUDGET_NAME_DUPLICATE`. Satisfies REQ-ERROR-TOAST-1.
- [ ] A-2.12 `ExecutionRecordForm` — add operationDate required + period-range check (best-effort when period dates available in props). Satisfies REQ-EXEC-DATE-VAL-1.
- [ ] A-2.13 `ExecutionRecordForm` — add decimal precision regex: amount ≤ 2 dp, exchangeRate ≤ 6 dp. Satisfies REQ-EXEC-DECIMAL-VAL-1.
- [ ] A-2.14 `ExecutionRecordForm` — remove inline `submitError` banner; wire all API errors to `toastStore.push` (codes: `OPERATION_DATE_OUT_OF_RANGE`, `NOTE_REQUIRED`, unknown fallback). Satisfies REQ-EXEC-TOAST-MIGRATE-1.
- [ ] A-2.15 `CycleListView` inline edit — add same validate() as CycleForm (nameRequired, nameTooLong); block submit if invalid. Satisfies REQ-CYCLE-LIST-INLINE-VAL-1.
- [ ] A-2.16 `frontend/src/i18n/locales/en.json` — add all ~28 new validation and error-toast keys. Satisfies REQ-I18N-1.
- [ ] A-2.17 `frontend/src/i18n/locales/es.json` — add matching ~28 keys in Spanish. Satisfies REQ-I18N-1.

---

## Phase 3: Testing

### Track B tests (TDD — RED then GREEN, co-located with Phase 1/2)

- [ ] B-3.1 Handler tests for `CreateCategoryGroup` / `UpdateCategoryGroup`: soft-deleted duplicate rejected (REQ-CG-01, REQ-CG-02).
- [ ] B-3.2 Handler tests for `CreateCategory` / `UpdateCategory`: soft-deleted duplicate rejected (REQ-CAT-01, REQ-CAT-02).
- [ ] B-3.3 Handler tests for `CreateBudget` / `RenameBudget`: active duplicate rejected, soft-deleted duplicate rejected (REQ-BUDGET-UNIQUE-1).
- [ ] B-3.4 Handler tests for `CreateCycle` / `UpdateCycle`: duplicate name rejected, self-rename allowed (REQ-CYC-NAME-1).
- [ ] B-3.5 Handler tests for `CreatePeriod` / `UpdatePeriod`: duplicate name rejected, self-rename allowed (REQ-PER-NAME-1).
- [ ] B-3.6 Handler tests for `CreateBudgetLine` / `UpdateBudgetLine`: duplicate name per (cg,cat) rejected, self-rename allowed (REQ-BL-NAME-1).
- [ ] B-3.7 Validator test `CreateBudgetLineValidator`: amount=0 rejected, amount=0.01 accepted (REQ-BL-AMOUNT-1).
- [ ] B-3.8 Validator tests `Create/UpdateExecutionRecordValidator`: Note absent for Expense rejected; Note absent for CreditNote rejected; Note present accepted (REQ-EXEC-4).
- [ ] B-3.9 Validator tests `Create/UpdateExecutionRecordValidator`: operationDate before period start rejected, after end rejected, within range accepted, null passes (REQ-EXEC-DATE-RANGE-1).

### Track A tests (TDD — RED then GREEN, co-located with Phase 2)

- [ ] A-3.1 Unit test `extractApiErrorCode`: AxiosError with `{ error: 'CODE' }` body, ProblemDetails `{ detail: 'CODE' }` body, non-Axios error → null (REQ-ERROR-TOAST-1).
- [ ] A-3.2 Unit tests for each form `validate()`: boundary values — empty name, 200-char name passes, 201-char name fails, amount=0 fails (REQ-FORM-INLINE-VAL-1, REQ-BL-AMOUNT-1).
- [ ] A-3.3 Unit tests `ExecutionRecordForm validate()`: 3-decimal amount blocked, 7-decimal rate blocked, valid precision passes (REQ-EXEC-DECIMAL-VAL-1).
- [ ] A-3.4 Component tests for CategoryGroupForm, CategoryForm, CycleForm, PeriodForm, BudgetLineModal: mock store to throw 422 with known code → assert `toastStore.push` called with correct key (REQ-ERROR-TOAST-1).
- [ ] A-3.5 Component test `ExecutionRecordForm`: mock store throw `OPERATION_DATE_OUT_OF_RANGE` → toast shown, no inline banner (REQ-EXEC-TOAST-MIGRATE-1, REQ-EXEC-DATE-VAL-1).
- [ ] A-3.6 Smoke test: i18n key coverage — every new key in en.json has a matching key in es.json (REQ-I18N-1).

---

## Phase 4: Cleanup

- [ ] C-4.1 Confirm `BudgetLineRevision.Note` max 200 — check existing data does not violate constraint before committing EF config change (open question from design).
- [ ] C-4.2 Remove any dead `submitError` ref/variable left in `ExecutionRecordForm` after banner removal.
- [ ] C-4.3 Verify no hardcoded English validation strings remain in CategoryGroupForm or CategoryForm (REQ-I18N-1).
- [ ] C-4.4 Run full test suite on both tracks; confirm zero regressions.
