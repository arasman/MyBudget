# Delta for budget-structure-ui

## MODIFIED Requirements

### Requirement: REQ-CYC-1 — Cycle List

The system MUST display all cycles for the active budget via `GET /api/budgets/{budgetId}/cycles`. Each row MUST show name, start date, end date, period count, and active status. When a cycle has an alternate currency, the list MUST also display the alternate currency symbol or code. An empty state with a guided prompt MUST be shown when no cycles exist.

(Previously: list rows showed no alternate currency information.)

#### Scenario: Cycles listed

- GIVEN the budget has two cycles
- WHEN the Cycles tab is active
- THEN both cycles are displayed with name, date range, period count, and active badge

#### Scenario: Empty state shown

- GIVEN the budget has no cycles
- WHEN the Cycles tab is active
- THEN a guided empty-state prompt is shown instead of an empty table

#### Scenario: Alternate currency shown when present

- GIVEN a cycle with alternateCurrency.code="USD"
- WHEN the Cycles tab renders
- THEN the cycle row displays the alternate currency symbol or code

#### Scenario: Alternate currency absent when not set

- GIVEN a cycle with alternateCurrency=null
- WHEN the Cycles tab renders
- THEN no alternate currency indicator is shown for that row

---

### Requirement: REQ-I18N-1 — Budget Structure i18n Keys

All user-visible strings in the budget structure UI MUST use keys under the `budgetStructure.*` namespace in `en.json` and `es.json`. No hardcoded English or Spanish strings MAY appear in template markup. The keys `budgetStructure.cycles.defaultCurrency`, `budgetStructure.cycles.alternateCurrency`, and `budgetStructure.cycles.exchangeRate` MUST be present in both locale files with appropriate translations.

(Previously: the three cycle currency keys were absent from both locale files.)

#### Scenario: All budget structure strings are i18n-keyed

- GIVEN the EN and ES locale files
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

---

## ADDED Requirements

### Requirement: REQ-CYC-TYPES-1 — CycleListItem and CycleDetail Type Extensions

`CycleListItem` and `CycleDetail` TypeScript types MUST include optional fields `alternateCurrencyId` (string | null), `exchangeRate` (number | null), and `alternateCurrency` (CurrencyItem | null).

#### Scenario: Type accepts null alternate fields

- GIVEN an API response for a cycle without alternate currency
- WHEN the response is parsed into CycleListItem
- THEN the type accepts alternateCurrencyId=null, exchangeRate=null, alternateCurrency=null without TypeScript error

#### Scenario: Type accepts populated alternate fields

- GIVEN an API response for a cycle with AlternateCurrencyId=USD, ExchangeRate=7.5
- WHEN the response is parsed into CycleListItem
- THEN alternateCurrencyId, exchangeRate, and alternateCurrency fields are accessible and typed correctly

---

### Requirement: REQ-CYC-FORM-1 — CycleForm Alternate Currency Inputs

`CycleForm.vue` MUST include an alternate currency dropdown and an exchange rate numeric input. The alternate currency dropdown MUST use the same currency list source as the default currency dropdown. The exchange rate input MUST only be enabled and visible when an alternate currency is selected. The exchange rate label MUST express direction as "X [defaultCurrencyCode] = 1 [alternateCurrencyCode]" using the currently selected currency codes.

#### Scenario: Exchange rate input hidden when no alternate currency

- GIVEN the CycleForm renders with no alternate currency selected
- WHEN the form is displayed
- THEN the exchange rate input is not visible or is disabled

#### Scenario: Exchange rate input shown when alternate currency selected

- GIVEN the user selects an alternate currency in the dropdown
- WHEN the form updates
- THEN the exchange rate input becomes visible and enabled
- AND the label reads "X [defaultCode] = 1 [alternateCode]"

#### Scenario: Exchange rate label reflects selected currencies

- GIVEN defaultCurrency=GTQ and alternateCurrency=USD selected
- WHEN the exchange rate label renders
- THEN the label reads "X GTQ = 1 USD" (or equivalent localized pattern)

---

### Requirement: REQ-CYC-FORM-2 — CycleForm Pair Validation

`CycleForm.vue` MUST enforce client-side pair validation: `alternateCurrencyId` and `exchangeRate` MUST both be filled or both empty. Submitting with only one field filled MUST be prevented with an inline validation message.

#### Scenario: Only alternate currency filled — submission blocked

- GIVEN the user selects an alternate currency but leaves exchange rate empty
- WHEN they attempt to submit the form
- THEN submission is blocked and an inline error is shown for the exchange rate field

#### Scenario: Only exchange rate filled — submission blocked

- GIVEN the user enters an exchange rate but leaves alternate currency unselected
- WHEN they attempt to submit the form
- THEN submission is blocked and an inline error is shown for the alternate currency field

#### Scenario: Both fields filled — submission allowed

- GIVEN the user has selected an alternate currency and entered a positive exchange rate
- WHEN they submit the form
- THEN no pair validation error is raised and the form submits

#### Scenario: Both fields empty — submission allowed

- GIVEN the user has left both alternate currency and exchange rate empty
- WHEN they submit the form
- THEN no pair validation error is raised

---

### Requirement: REQ-CYC-DETAIL-1 — CycleDetailView Alternate Currency Display

`CycleDetailView.vue` MUST display the cycle's alternate currency info and exchange rate when present. The exchange rate display MUST reflect direction semantics in the format "X [defaultCurrencyCode] = 1 [alternateCurrencyCode]".

#### Scenario: Alternate currency section shown when present

- GIVEN a cycle with alternateCurrency.code="USD" and exchangeRate=7.5 and defaultCurrency.code="GTQ"
- WHEN the cycle detail view renders
- THEN the alternate currency section is visible and shows "7.5 GTQ = 1 USD"

#### Scenario: Alternate currency section absent when not set

- GIVEN a cycle with alternateCurrency=null
- WHEN the cycle detail view renders
- THEN no alternate currency section or exchange rate is displayed
