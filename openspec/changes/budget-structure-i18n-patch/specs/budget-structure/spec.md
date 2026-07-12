# Delta for budget-structure

## MODIFIED Requirements

### Requirement: REQ-CYC-CUR-02 — Cycle Read Responses

`GetCycleDetail` response MUST include `defaultCurrency` (Code, Symbol), and optionally `alternateCurrency` (Code, Symbol) and `exchangeRate`.

`ListCycles` response items MUST include `defaultCurrency` (Code, Symbol), and optionally `alternateCurrencyId` (Guid?), `exchangeRate` (decimal?), and `alternateCurrency` (object with Code and Symbol, nullable).

(Previously: `ListCycles` response items included only `defaultCurrency`; `alternateCurrencyId`, `exchangeRate`, and `alternateCurrency` were absent from the list projection.)

#### Scenario: Detail with alternate currency `@integration`

- GIVEN Cycle with DefaultCurrencyId=GTQ, AlternateCurrencyId=USD, ExchangeRate=7.5
- WHEN GET `/api/budgets/{id}/cycles/{cycleId}`
- THEN HTTP 200; defaultCurrency: { code: "GTQ", symbol: "Q" }, alternateCurrency: { code: "USD", symbol: "$" }, exchangeRate: 7.5

#### Scenario: List includes alternate currency when present `@integration`

- GIVEN a Cycle with DefaultCurrencyId=GTQ, AlternateCurrencyId=USD, ExchangeRate=7.5
- WHEN GET `/api/budgets/{id}/cycles`
- THEN each matching item includes alternateCurrencyId, exchangeRate, and alternateCurrency: { code: "USD", symbol: "$" }

#### Scenario: List item has null alternate fields when not set `@integration`

- GIVEN a Cycle with only DefaultCurrencyId set and no AlternateCurrencyId
- WHEN GET `/api/budgets/{id}/cycles`
- THEN the item has alternateCurrencyId=null, exchangeRate=null, alternateCurrency=null
