# Spec: budget-structure-i18n-patch

## Domains

| Domain | Type | Requirements |
|---|---|---|
| `budget-structure` | Delta | 1 modified (REQ-CYC-CUR-02) |
| `budget-structure-ui` | Delta | 2 modified (REQ-CYC-1, REQ-I18N-1) + 4 added (REQ-CYC-TYPES-1, REQ-CYC-FORM-1, REQ-CYC-FORM-2, REQ-CYC-DETAIL-1) |

See domain delta files:
- `specs/budget-structure/spec.md`
- `specs/budget-structure-ui/spec.md`

---

## Domain: budget-structure

### MODIFIED Requirements

### Requirement: REQ-CYC-CUR-02 — Cycle Read Responses

`GetCycleDetail` response MUST include `defaultCurrency` (Code, Symbol), and optionally `alternateCurrency` (Code, Symbol) and `exchangeRate`.

`ListCycles` response items MUST include `defaultCurrency` (Code, Symbol), and optionally `alternateCurrencyId` (Guid?), `exchangeRate` (decimal?), and `alternateCurrency` (object with Code and Symbol, nullable).

(Previously: `ListCycles` items included only `defaultCurrency`; alternate fields were absent from the list projection.)

#### Scenario: Detail with alternate currency `@integration`

- GIVEN Cycle with DefaultCurrencyId=GTQ, AlternateCurrencyId=USD, ExchangeRate=7.5
- WHEN GET `/api/budgets/{id}/cycles/{cycleId}`
- THEN HTTP 200; defaultCurrency: { code: "GTQ", symbol: "Q" }, alternateCurrency: { code: "USD", symbol: "$" }, exchangeRate: 7.5

#### Scenario: List includes alternate currency when present `@integration`

- GIVEN a Cycle with AlternateCurrencyId=USD, ExchangeRate=7.5
- WHEN GET `/api/budgets/{id}/cycles`
- THEN each matching item includes alternateCurrencyId, exchangeRate, and alternateCurrency: { code: "USD", symbol: "$" }

#### Scenario: List item has null alternate fields when not set `@integration`

- GIVEN a Cycle with only DefaultCurrencyId set
- WHEN GET `/api/budgets/{id}/cycles`
- THEN the item has alternateCurrencyId=null, exchangeRate=null, alternateCurrency=null

---

## Domain: budget-structure-ui

### MODIFIED Requirements

### Requirement: REQ-CYC-1 — Cycle List

The system MUST display all cycles via `GET /api/budgets/{budgetId}/cycles`. Each row MUST show name, start date, end date, period count, and active status. When a cycle has an alternate currency, the list MUST also display the alternate currency symbol or code. Empty state MUST be shown when no cycles exist.

(Previously: list rows showed no alternate currency information.)

#### Scenario: Cycles listed

- GIVEN the budget has two cycles
- WHEN the Cycles tab is active
- THEN both cycles are displayed with name, date range, period count, and active badge

#### Scenario: Empty state shown

- GIVEN the budget has no cycles
- WHEN the Cycles tab is active
- THEN a guided empty-state prompt is shown

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

All user-visible strings MUST use `budgetStructure.*` keys. The keys `budgetStructure.cycles.defaultCurrency`, `budgetStructure.cycles.alternateCurrency`, and `budgetStructure.cycles.exchangeRate` MUST be present in both `en.json` and `es.json`.

(Previously: the three cycle currency keys were absent from both locale files.)

#### Scenario: All budget structure strings are i18n-keyed

- GIVEN the EN and ES locale files contain all `budgetStructure.*` keys
- WHEN the budget structure UI renders in either locale
- THEN no i18n fallback warnings appear

#### Scenario: Currency i18n keys resolve in English

- GIVEN locale is "en"
- WHEN cycle currency labels render
- THEN all three currency keys resolve to non-empty English strings

#### Scenario: Currency i18n keys resolve in Spanish

- GIVEN locale is "es"
- WHEN cycle currency labels render
- THEN all three currency keys resolve to non-empty Spanish strings

---

### ADDED Requirements

### Requirement: REQ-CYC-TYPES-1 — CycleListItem and CycleDetail Type Extensions

`CycleListItem` and `CycleDetail` TypeScript types MUST include optional fields `alternateCurrencyId` (string | null), `exchangeRate` (number | null), and `alternateCurrency` (CurrencyItem | null).

#### Scenario: Type accepts null alternate fields

- GIVEN an API response for a cycle without alternate currency
- WHEN parsed into CycleListItem
- THEN alternateCurrencyId=null, exchangeRate=null, alternateCurrency=null are accepted without TypeScript error

#### Scenario: Type accepts populated alternate fields

- GIVEN an API response with AlternateCurrencyId=USD, ExchangeRate=7.5
- WHEN parsed into CycleListItem
- THEN the three fields are accessible and correctly typed

---

### Requirement: REQ-CYC-FORM-1 — CycleForm Alternate Currency Inputs

`CycleForm.vue` MUST include an alternate currency dropdown (same source as default currency) and an exchange rate numeric input. The exchange rate input MUST only be enabled and visible when an alternate currency is selected. The label MUST express direction as "X [defaultCurrencyCode] = 1 [alternateCurrencyCode]".

#### Scenario: Exchange rate input hidden with no alternate currency selected

- GIVEN CycleForm renders with no alternate currency
- WHEN the form displays
- THEN the exchange rate input is not visible or is disabled

#### Scenario: Exchange rate input shown when alternate currency selected

- GIVEN the user selects an alternate currency
- WHEN the form updates
- THEN the exchange rate input becomes visible and enabled, with label "X [defaultCode] = 1 [alternateCode]"

---

### Requirement: REQ-CYC-FORM-2 — CycleForm Pair Validation

Client-side pair validation MUST prevent form submission when only one of `alternateCurrencyId` or `exchangeRate` is filled. Both filled or both empty MUST be allowed.

#### Scenario: Only alternate currency filled — blocked

- GIVEN alternate currency selected, exchange rate empty
- WHEN user attempts to submit
- THEN submission is blocked with an inline validation error

#### Scenario: Only exchange rate filled — blocked

- GIVEN exchange rate entered, alternate currency unselected
- WHEN user attempts to submit
- THEN submission is blocked with an inline validation error

#### Scenario: Both filled — allowed

- GIVEN alternate currency selected and positive exchange rate entered
- WHEN user submits
- THEN no pair validation error; form submits

#### Scenario: Both empty — allowed

- GIVEN both fields empty
- WHEN user submits
- THEN no pair validation error

---

### Requirement: REQ-CYC-DETAIL-1 — CycleDetailView Alternate Currency Display

`CycleDetailView.vue` MUST display alternate currency and exchange rate when present. The display MUST follow the format "X [defaultCurrencyCode] = 1 [alternateCurrencyCode]".

#### Scenario: Section shown when alternate currency present

- GIVEN cycle with alternateCurrency.code="USD", exchangeRate=7.5, defaultCurrency.code="GTQ"
- WHEN cycle detail renders
- THEN the display shows "7.5 GTQ = 1 USD"

#### Scenario: Section absent when not set

- GIVEN cycle with alternateCurrency=null
- WHEN cycle detail renders
- THEN no alternate currency section is displayed
