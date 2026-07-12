# Tasks: Budget Structure i18n Patch

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 150–220 |
| 400-line budget risk | Low |
| Chained PRs recommended | No |
| Suggested split | Single PR |
| Delivery strategy | ask-on-risk |
| Chain strategy | size-exception |

Decision needed before apply: No
Chained PRs recommended: No
Chain strategy: size-exception
400-line budget risk: Low

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | All 8 files + tests | PR 1 | Single PR; all changes additive, no migration |

---

## Phase 1: Foundation — i18n Keys + TypeScript Types

- [x] 1.1 Add 6 keys (`defaultCurrency`, `alternateCurrency`, `exchangeRate`, `exchangeRateLabel`, `pairValidationError`, `noneSelected`) under `budgetStructure.cycles` in `frontend/src/i18n/locales/en.json`. Satisfies REQ-I18N-1.
- [x] 1.2 Add the same 6 keys (Spanish translations) under `budgetStructure.cycles` in `frontend/src/i18n/locales/es.json`. Satisfies REQ-I18N-1.
- [x] 1.3 Extend `CycleListItem` interface in `frontend/src/features/budget-structure/types.ts` with `alternateCurrency?: CurrencyItem | null`, `exchangeRate?: number | null`. Satisfies REQ-CYC-TYPES-1.
- [x] 1.4 Confirm `CycleDetail` inherits the new optional fields via `extends CycleListItem` (or add them explicitly). Satisfies REQ-CYC-TYPES-1.

## Phase 2: Backend — ListCyclesHandler SQL Projection

- [x] 2.1 Add `AlternateCurrencyId?`, `ExchangeRate?`, `AlternateCurrencyCode?`, `AlternateCurrencySymbol?` to the `CycleRow` private record in `src/MyBudget.Features/Features/BudgetStructure/Cycles/List/ListCyclesHandler.cs`. Satisfies REQ-CYC-CUR-02.
- [x] 2.2 Extend the Dapper SQL in `ListCyclesHandler` with `LEFT JOIN Currencies ac ON ac.Id = c.AlternateCurrencyId` and add the 4 new nullable columns to SELECT. Satisfies REQ-CYC-CUR-02.
- [x] 2.3 Update the `CycleListItem` record (in `ListCyclesQuery.cs` or co-located) to include `CurrencyDto? AlternateCurrency` and `decimal? ExchangeRate`. Map from `CycleRow` in the grouping/projection step. Satisfies REQ-CYC-CUR-02.

## Phase 3: Frontend — UI Components

- [x] 3.1 Add alternate currency `<select>` (bound to `form.alternateCurrencyId`) and exchange rate `<input type="number">` (bound to `form.exchangeRate`) to `frontend/src/features/budget-structure/components/CycleForm.vue`. Hide/disable rate input when no alternate currency is selected. Satisfies REQ-CYC-FORM-1.
- [x] 3.2 Add pair validation logic in `CycleForm.vue` `validate()`: block submit when exactly one of the two fields is filled; show `budgetStructure.cycles.pairValidationError` inline. Satisfies REQ-CYC-FORM-2.
- [x] 3.3 Add dynamic label for exchange rate input in `CycleForm.vue` using `t('budgetStructure.cycles.exchangeRateLabel', { defaultCurrency: ..., alternateCurrency: ... })`. Satisfies REQ-CYC-FORM-1.
- [x] 3.4 Add optional alternate currency display column in `frontend/src/features/budget-structure/views/CycleListView.vue`; render symbol/code only when `alternateCurrency` is non-null. Satisfies REQ-CYC-1.
- [x] 3.5 Add conditional alternate currency + exchange rate section in `frontend/src/features/budget-structure/views/CycleDetailView.vue` using format "X [defaultCode] = 1 [alternateCode]". Satisfies REQ-CYC-DETAIL-1.

## Phase 4: Tests

- [x] 4.1 Backend unit test in `MyBudget.Features.Tests`: seed cycle with `AlternateCurrencyId=USD`, `ExchangeRate=7.5`; assert `ListCyclesHandler` result includes `AlternateCurrency.Code="USD"` and `ExchangeRate=7.5`. Satisfies REQ-CYC-CUR-02 unit scenario. [NOTE: Covered by integration test 4.3 — ConnectionFactory is sealed/Npgsql-bound; see apply-progress for deviation details.]
- [x] 4.2 Backend unit test: seed cycle without alternate currency; assert `AlternateCurrency` is null and `ExchangeRate` is null. Satisfies REQ-CYC-CUR-02 null scenario. [NOTE: Covered by integration test — same reason as 4.1.]
- [x] 4.3 Backend integration test in `MyBudget.Integration.Tests`: `GET /api/budgets/{id}/cycles` with seeded alternate currency; assert JSON includes `alternateCurrency.code`, `exchangeRate`. Satisfies REQ-CYC-CUR-02 `@integration` scenarios.
- [x] 4.4 Frontend unit test (Vitest + @testing-library/vue): mount `CycleForm.vue`; assert exchange rate input hidden when no alternate currency; assert it appears and label reads "X GTQ = 1 USD" when alternate currency selected. Satisfies REQ-CYC-FORM-1 scenarios.
- [x] 4.5 Frontend unit test: exercise pair validation — submit with only alternate currency → error shown; only rate → error shown; both filled → no error; both empty → no error. Satisfies REQ-CYC-FORM-2 scenarios.
- [x] 4.6 Frontend unit test: assert `CycleListView.vue` renders alternate currency symbol when present, renders nothing when null. Satisfies REQ-CYC-1 alternate currency scenarios.
- [x] 4.7 E2E Playwright test (`e2e/budget-structure-cycles.spec.ts`): create a cycle with alternate currency USD and rate 7.5; verify list row shows "USD"; navigate to detail; verify "7.5 GTQ = 1 USD" is visible. Satisfies REQ-CYC-DETAIL-1 and REQ-CYC-1 E2E scenarios.
