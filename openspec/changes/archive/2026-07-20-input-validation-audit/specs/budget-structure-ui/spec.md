# Delta for budget-structure-ui

## MODIFIED Requirements

### Requirement: REQ-I18N-1 — Budget Structure i18n Keys

All user-visible strings in the budget structure UI MUST use keys under the `budgetStructure.*`
namespace in `en.json` and `es.json`. No hardcoded English or Spanish strings MAY appear in
template markup. The keys `budgetStructure.cycles.defaultCurrency`,
`budgetStructure.cycles.alternateCurrency`, and `budgetStructure.cycles.exchangeRate` MUST be
present in both locale files with appropriate translations.

Additionally, the following validation inline-error keys MUST be present in both locale files:
- `budgetStructure.categoryGroups.validation.nameRequired`
- `budgetStructure.categoryGroups.validation.nameTooLong`
- `budgetStructure.categories.validation.nameRequired`
- `budgetStructure.categories.validation.nameTooLong`
- `budgetStructure.cycles.validation.nameRequired`
- `budgetStructure.cycles.validation.nameTooLong`
- `budgetStructure.periods.validation.nameRequired`
- `budgetStructure.periods.validation.nameTooLong`
- `budgetStructure.periods.validation.startDateRequired`
- `budgetStructure.periods.validation.endDateRequired`
- `budgetStructure.periods.validation.dateOrder`
- `budgetStructure.budgetLines.validation.nameRequired`
- `budgetStructure.budgetLines.validation.nameTooLong`
- `budgetStructure.budgetLines.validation.amountRequired`
- `budgetStructure.budgetLines.validation.amountPositive`

The following error-toast keys MUST also be present in both locale files:
- `budgetStructure.selection.budgetNameDuplicate`
- `budgetStructure.categoryGroups.errors.nameDuplicate`
- `budgetStructure.categories.errors.nameDuplicate`
- `budgetStructure.cycles.errors.dateOverlap`
- `budgetStructure.cycles.errors.nameDuplicate`
- `budgetStructure.periods.errors.nameDuplicate`
- `budgetStructure.periods.errors.outOfCycleRange`
- `budgetStructure.periods.errors.dateOverlap`
- `budgetStructure.budgetLines.errors.nameDuplicate`

(Previously: ~15 validation and error-toast keys were missing; CategoryGroupForm and CategoryForm used hardcoded English strings)

#### Scenario: All budget structure strings are i18n-keyed

- GIVEN the EN and ES locale files with the full key set
- WHEN all `budgetStructure.*` keys are present
- THEN rendering the budget structure UI in either locale shows fully translated text with no fallback warnings

#### Scenario: Currency i18n keys resolve in English

- GIVEN locale is "en"
- WHEN the cycle form or detail view renders currency labels
- THEN `budgetStructure.cycles.defaultCurrency`, `budgetStructure.cycles.alternateCurrency`, and `budgetStructure.cycles.exchangeRate` each resolve to a non-empty English string

#### Scenario: Currency i18n keys resolve in Spanish

- GIVEN locale is "es"
- WHEN the cycle form or detail view renders currency labels
- THEN the three currency keys each resolve to a non-empty Spanish string

#### Scenario: Validation i18n keys resolve in both locales

- GIVEN locale is "en" or "es"
- WHEN inline validation triggers in any structure form
- THEN the displayed message uses a translated string, not a hardcoded English literal

---

## ADDED Requirements

### Requirement: REQ-FORM-INLINE-VAL-1 — Inline Validation on Structure Forms

All six structure forms MUST perform client-side inline validation before submitting. Validation
MUST block form submission and display inline messages at the field level using i18n keys.

| Form | Field | Rules |
|---|---|---|
| CategoryGroupForm | name | required, max 200 |
| CategoryForm | name | required, max 200 |
| CycleForm | name | required, max 200 |
| PeriodForm | name | required, max 200 |
| PeriodForm | startDate | required, < endDate |
| PeriodForm | endDate | required, > startDate |
| BudgetLineModal | name | required, max 200 |
| BudgetLineModal | amount | required, > 0 |

#### Scenario: CategoryGroupForm name required `@unit`
- GIVEN an empty name field in CategoryGroupForm
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetStructure.categoryGroups.validation.nameRequired` is shown inline

#### Scenario: CategoryGroupForm name too long `@unit`
- GIVEN a name exceeding 200 characters in CategoryGroupForm
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetStructure.categoryGroups.validation.nameTooLong` is shown inline

#### Scenario: BudgetLineModal amount must be positive `@unit`
- GIVEN amount = 0 in BudgetLineModal
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetStructure.budgetLines.validation.amountPositive` is shown inline

#### Scenario: PeriodForm date order enforced `@unit`
- GIVEN startDate is after or equal to endDate in PeriodForm
- WHEN the user attempts to submit
- THEN submission is blocked and `budgetStructure.periods.validation.dateOrder` is shown inline

---

### Requirement: REQ-WRAP-RETHROW-1 — store._wrap() Re-throws Errors

The `_wrap()` utility in `budget-structure/store.ts` MUST re-throw errors after logging or setting
`store.error`, so that callers can catch API errors via `try/catch`.

#### Scenario: API error propagates to caller `@unit`
- GIVEN a store action wrapped in `_wrap()` whose API call returns 422
- WHEN the view awaits the store action
- THEN the error is not silently swallowed — it propagates to the awaiting caller's catch block

---

### Requirement: REQ-ERROR-TOAST-1 — Error Toasts on Business Rule Violations

View action handlers for all structure entities MUST wrap store calls in `try/catch` and push
an error toast via `toastStore.push({ type: 'error', title: t(key) })` when the API returns a
business-rule error code. The mapping of API error codes to i18n keys MUST be:

| API Error Code | i18n Key |
|---|---|
| `BUDGET_NAME_DUPLICATE` | `budgetStructure.selection.budgetNameDuplicate` |
| `CATEGORY_GROUP_NAME_DUPLICATE` | `budgetStructure.categoryGroups.errors.nameDuplicate` |
| `CATEGORY_NAME_DUPLICATE` | `budgetStructure.categories.errors.nameDuplicate` |
| `CYCLE_DATE_OVERLAP` | `budgetStructure.cycles.errors.dateOverlap` |
| `CYCLE_NAME_DUPLICATE` | `budgetStructure.cycles.errors.nameDuplicate` |
| `PERIOD_NAME_DUPLICATE` | `budgetStructure.periods.errors.nameDuplicate` |
| `PERIOD_OUT_OF_CYCLE_RANGE` | `budgetStructure.periods.errors.outOfCycleRange` |
| `PERIOD_DATE_OVERLAP` | `budgetStructure.periods.errors.dateOverlap` |
| `BUDGET_LINE_NAME_DUPLICATE` | `budgetStructure.budgetLines.errors.nameDuplicate` |

#### Scenario: Duplicate category group name shows error toast `@unit`
- GIVEN the user submits CategoryGroupForm with a duplicate name
- WHEN the API returns 422 with code `CATEGORY_GROUP_NAME_DUPLICATE`
- THEN `toastStore.push({ type: 'error', title: t('budgetStructure.categoryGroups.errors.nameDuplicate') })` is called

#### Scenario: No silent failure on cycle date overlap `@unit`
- GIVEN the user submits CycleForm with overlapping dates
- WHEN the API returns 422 with code `CYCLE_DATE_OVERLAP`
- THEN an error toast is shown and no success toast is emitted

#### Scenario: No error toast on successful create `@unit`
- GIVEN the user submits any structure form successfully
- WHEN the API returns 201 or 200
- THEN no error toast is pushed

---

### Requirement: REQ-CYCLE-LIST-INLINE-VAL-1 — CycleListView Inline Edit Validation

The inline edit in `CycleListView.vue` MUST apply the same validation as `CycleForm.vue`:
name required, name max 200, startDate required, endDate required, endDate > startDate.

#### Scenario: Inline edit — empty name blocked `@unit`
- GIVEN the user clears the cycle name in the inline edit row
- WHEN they attempt to save
- THEN the save is blocked and an inline error is shown (using `budgetStructure.cycles.validation.nameRequired`)

#### Scenario: Inline edit — invalid date order blocked `@unit`
- GIVEN the user sets endDate before startDate in the inline edit row
- WHEN they attempt to save
- THEN the save is blocked and an inline error is shown (using `budgetStructure.periods.validation.dateOrder` or equivalent cycle key)
