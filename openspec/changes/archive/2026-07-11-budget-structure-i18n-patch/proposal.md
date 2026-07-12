# Proposal: Budget Structure i18n Patch

## Intent

The Cycle entity backend supports `AlternateCurrencyId` and `ExchangeRate` (with pair validation), but the frontend is incomplete: missing i18n keys, missing TypeScript type fields, no form inputs, and no display in list/detail views. Users cannot configure or see alternate currency information despite the backend being ready.

## Scope

### In Scope
- Add 3 missing i18n keys (`defaultCurrency`, `alternateCurrency`, `exchangeRate`) to `en.json` and `es.json`
- Add `alternateCurrencyId` and `exchangeRate` to `ListCyclesHandler` Dapper SQL projection
- Add `alternateCurrencyId`, `exchangeRate`, and `alternateCurrency` fields to `CycleListItem` and `CycleDetail` TypeScript types
- Add alternate currency dropdown + exchange rate numeric input to `CycleForm.vue` with pair validation (both or neither)
- Display alternate currency and exchange rate in `CycleListView.vue` and `CycleDetailView.vue`
- Exchange rate label reflects semantics: "X defaultCurrency = 1 alternateCurrency"

### Out of Scope
- Backend entity changes (already complete)
- New migrations (already applied)
- Pair validation logic changes (already enforced via `CYC_PAIR_INCOMPLETE`)
- New API endpoints

## Capabilities

### New Capabilities
None

### Modified Capabilities
- `budget-structure`: Add alternate currency fields to list query handler, frontend types, form, and views

## Approach

1. **Backend**: Update `ListCyclesHandler` SQL to SELECT `AlternateCurrencyId` and `ExchangeRate`; update the list DTO
2. **i18n**: Add 3 keys to both locale files under `budgetStructure.cycles`
3. **Types**: Extend `CycleListItem` and `CycleDetail` with optional `alternateCurrencyId`, `exchangeRate`, and `alternateCurrency` (CurrencyItem)
4. **Form**: Add currency dropdown (reuse existing currency list) + numeric input; implement client-side pair validation mirroring backend rule
5. **Views**: Conditionally display alternate currency info when present

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/src/i18n/locales/en.json` | Modified | Add 3 i18n keys |
| `frontend/src/i18n/locales/es.json` | Modified | Add 3 i18n keys |
| `src/MyBudget.Features/Features/BudgetStructure/Cycles/List/ListCyclesHandler.cs` | Modified | Add columns to SQL projection |
| `frontend/src/features/budget-structure/types.ts` | Modified | Extend CycleListItem, CycleDetail |
| `frontend/src/features/budget-structure/components/CycleForm.vue` | Modified | Add currency + rate inputs |
| `frontend/src/features/budget-structure/views/CycleListView.vue` | Modified | Display alternate currency |
| `frontend/src/features/budget-structure/views/CycleDetailView.vue` | Modified | Display alternate currency |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| ListCyclesHandler SQL join adds complexity | Low | Alternate currency is optional; LEFT JOIN with null coalesce |
| Pair validation mismatch frontend vs backend | Low | Mirror exact backend rule; backend remains authoritative |

## Rollback Plan

Revert the feature branch. No migrations or schema changes involved; all changes are additive frontend + one query handler projection update.

## Dependencies

- Existing currency list endpoint (already available for default currency dropdown)
- Backend pair validation (`CYC_PAIR_INCOMPLETE`) already enforced

## Success Criteria

- [ ] `CycleForm.vue` allows setting alternate currency + exchange rate as a pair
- [ ] Form enforces pair validation: both or neither filled
- [ ] List and detail views display alternate currency and exchange rate when present
- [ ] All 3 i18n keys resolve in both English and Spanish
- [ ] Existing tests continue passing; new tests cover pair validation UI
