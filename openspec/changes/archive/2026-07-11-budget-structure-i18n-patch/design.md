# Design: Budget Structure i18n Patch

## Technical Approach

Extend the existing `ListCyclesHandler` Dapper query with a LEFT JOIN to the Currencies table for alternate currency, mirror the pattern already used in `GetCycleDetailHandler`. On the frontend, add optional fields to `CycleListItem`/`CycleDetail` types, extend `CycleForm.vue` with pair-validated inputs, and conditionally display alternate currency info in list/detail views. All user-facing strings go through vue-i18n keys.

## Architecture Decisions

| Decision | Choice | Alternatives Rejected | Rationale |
|----------|--------|----------------------|-----------|
| SQL pattern for alternate currency in list | LEFT JOIN + nullable columns (same as `GetCycleDetailHandler`) | Separate API call for currency lookup | Follows existing codebase pattern; single round-trip; nullable columns handle absent alternate currency cleanly |
| Pair validation location | Client-side mirror + backend authoritative (`CYC_PAIR_INCOMPLETE`) | Backend-only validation | UX requires instant feedback; backend remains source of truth for safety |
| Exchange rate label | Dynamic interpolation: `t('...exchangeRateLabel', { default: X, alternate: Y })` | Static label | Semantics change per currency pair; interpolation makes the "7.5 GTQ = 1 USD" pattern explicit |
| Form state for alternate currency | Extend existing `reactive()` form object with 2 fields | Separate composable for currency pair | Change is small; a composable would over-engineer for 2 fields + 1 validation rule |

## Data Flow

```
ListCyclesHandler (Dapper SQL)
  |-- LEFT JOIN Currencies ac ON ac.Id = c.AlternateCurrencyId
  |-- SELECT ac.Code, ac.Symbol, c.ExchangeRate, c.AlternateCurrencyId
  v
CycleListItem record (adds AlternateCurrency? + ExchangeRate?)
  v
API JSON response
  v
Frontend CycleListItem type (adds alternateCurrency? + exchangeRate?)
  v
CycleListView.vue  -- conditional column display
CycleDetailView.vue -- conditional info display
CycleForm.vue      -- dropdown + numeric input with pair validation
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `src/.../ListCycles/ListCyclesQuery.cs` | Modify | Add `AlternateCurrency?` and `ExchangeRate?` to `CycleListItem` record |
| `src/.../ListCycles/ListCyclesHandler.cs` | Modify | Add LEFT JOIN for alternate currency; add `AlternateCurrencyId`, `ExchangeRate`, `AlternateCurrencyCode`, `AlternateCurrencySymbol` to SQL SELECT, GROUP BY, and `CycleRow`; map to `CycleListItem` |
| `frontend/src/features/budget-structure/types.ts` | Modify | Add `alternateCurrency?: CurrencyItem`, `exchangeRate?: number` to `CycleListItem`; same to `CycleDetail` |
| `frontend/src/features/budget-structure/components/CycleForm.vue` | Modify | Add alternate currency `<select>` + exchange rate `<input type="number">`; add pair validation; add dynamic exchange rate label; extend `CycleFormPayload` and emit with new fields |
| `frontend/src/features/budget-structure/views/CycleListView.vue` | Modify | Add optional column for alternate currency + exchange rate display |
| `frontend/src/features/budget-structure/views/CycleDetailView.vue` | Modify | Display alternate currency + exchange rate in cycle header when present |
| `frontend/src/i18n/locales/en.json` | Modify | Add i18n keys under `budgetStructure.cycles` |
| `frontend/src/i18n/locales/es.json` | Modify | Add i18n keys under `budgetStructure.cycles` |

## Interfaces / Contracts

### Backend: `CycleListItem` record (modified)

```csharp
public sealed record CycleListItem(
    Guid        Id,
    string      Name,
    DateOnly    StartDate,
    DateOnly    EndDate,
    bool        IsActive,
    int         PeriodCount,
    CurrencyDto DefaultCurrency,
    CurrencyDto? AlternateCurrency,  // NEW
    decimal?    ExchangeRate);       // NEW
```

### Backend: `CycleRow` (modified — add 3 nullable columns)

```csharp
private sealed record CycleRow(
    Guid     Id, string Name, DateOnly StartDate, DateOnly EndDate,
    bool     IsActive, long PeriodCount,
    string   DefaultCurrencyCode, string DefaultCurrencySymbol,
    Guid?    AlternateCurrencyId,       // NEW
    decimal? ExchangeRate,              // NEW
    string?  AlternateCurrencyCode,     // NEW
    string?  AlternateCurrencySymbol);  // NEW
```

### Frontend: TypeScript types (modified)

```typescript
export interface CycleListItem {
  // ... existing fields ...
  alternateCurrency?: CurrencyItem  // NEW
  exchangeRate?: number             // NEW
}

export interface CycleDetail extends Omit<CycleListItem, 'periodCount'> {
  periods: PeriodSummary[]
  // inherits alternateCurrency + exchangeRate from CycleListItem
}
```

### Frontend: i18n keys to add

```json
// en.json — under budgetStructure.cycles
"defaultCurrency": "Default Currency",
"alternateCurrency": "Alternate Currency",
"exchangeRate": "Exchange Rate",
"exchangeRateLabel": "{defaultCurrency} per 1 {alternateCurrency}",
"pairValidationError": "Both alternate currency and exchange rate are required, or leave both empty",
"noneSelected": "— None —"

// es.json — under budgetStructure.cycles
"defaultCurrency": "Moneda predeterminada",
"alternateCurrency": "Moneda alterna",
"exchangeRate": "Tipo de cambio",
"exchangeRateLabel": "{defaultCurrency} por 1 {alternateCurrency}",
"pairValidationError": "Ambos campos (moneda alterna y tipo de cambio) son requeridos, o deja ambos vacíos",
"noneSelected": "— Ninguna —"
```

### CycleForm pair validation logic

```typescript
// In validate():
const hasAlternate = !!form.alternateCurrencyId
const hasRate = form.exchangeRate != null && form.exchangeRate > 0
if (hasAlternate !== hasRate) {
  errors.pairValidation = t('budgetStructure.cycles.pairValidationError')
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit (backend) | `ListCyclesHandler` returns alternate currency fields; null when absent | In-memory SQLite; seed cycle with/without alternate currency |
| Unit (frontend) | Pair validation in CycleForm: both filled, both empty, one missing | Vitest + @testing-library/vue; assert error shown/hidden |
| Unit (frontend) | Dynamic exchange rate label interpolation | Vitest; mount CycleForm with currencies, verify label text |
| Integration | `GET /api/budgets/{id}/cycles` returns alternate currency in JSON | WebApplicationFactory; seed cycle with alternate currency |
| E2E | Create cycle with alternate currency; verify list shows it | Playwright; fill form, save, check table cell |

## Migration / Rollout

No migration required. Backend entity and schema already support `AlternateCurrencyId` and `ExchangeRate`. All changes are additive to the query projection and frontend layer.

## Open Questions

None. All design decisions confirmed by the orchestrator.
