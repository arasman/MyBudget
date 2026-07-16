# Design: Budget Execution Multi-Currency

## Technical Approach

Frontend-only changes across 4 components + 1 store + 2 locale files + 2 test fixtures. No new backend endpoints. The exchange rate save reuses the existing `PUT /cycles/{id}` via `structureStore.updateCycle()`. Currency symbol resolution leverages `CurrencyItem.symbol` already available on `structureStore.currentCycle.defaultCurrency` / `alternateCurrency`. The W-001 refactor moves subtotal logic into store getters so `MatrixTotalRow` derives from the same data path as `MatrixSummaryRow`.

## Architecture Decisions

| # | Decision | Alternatives Rejected | Rationale |
|---|----------|----------------------|-----------|
| D1 | Reuse `PUT /cycles` for exchange rate save | New `PATCH /exchange-rate` endpoint | Cycle data already in store; avoids backend work; rate-only updates pass validation (same dates) |
| D2 | Read currency symbol from `structureStore.currentCycle.defaultCurrency.symbol` / `alternateCurrency.symbol` | Fetch currencies from `/budgets/{id}/currencies` | `CycleDetail` extends `CycleListItem` which includes `CurrencyItem` with `symbol` field; no extra API call needed |
| D3 | Re-fetch `loadCycleDetail()` after `updateCycle()` to sync `currentCycle` | Patch `currentCycle` in-place inside `updateCycle()` | `updateCycle()` only updates `cycles[]` list items (line 82-85), not `currentCycle` ref; re-fetch is the safe path and also serves as freshness guard |
| D4 | Store-level `subtotalByLineType()` getter for W-001 | Emit subtotals from `MatrixSummaryRow` to parent | Store getter is single source of truth; avoids prop-drilling between sibling `tfoot` rows |
| D5 | `useCurrencyDisplay` reads `exchangeRate` from `matrixStore.exchangeRate` (current behavior) | Read from `structureStore.currentCycle.exchangeRate` | Changing the source would break the existing composable contract; instead, sync `matrixStore.exchangeRate` after rate save |

## Data Flow

### Exchange Rate Save

```
User edits rate in MatrixControls
  |
  v
localExchangeRate (local ref, v-model on input)
  |  blur / Enter
  v
structureStore.loadCycleDetail(budgetId, cycleId)  -- freshness guard
  |
  v
structureStore.updateCycle(budgetId, cycleId, { ...currentCycle fields, exchangeRate: newRate })
  |
  v
structureStore.loadCycleDetail(budgetId, cycleId)  -- re-fetch updated cycle
  |
  v
matrixStore.exchangeRate = structureStore.currentCycle.exchangeRate  -- sync to matrix store
  |
  v
All useCurrencyDisplay.convert() calls re-compute (reactive via matrixStore.exchangeRate ref)
```

### Currency Symbol Resolution

```
structureStore.currentCycle
  |
  +-- defaultCurrency.symbol (e.g. "Q")
  +-- alternateCurrency.symbol (e.g. "$")
  |
  v
MatrixCell reads matrixStore.displayCurrency
  |
  v
computed currencySymbol = displayCurrency === 'alternate'
    ? currentCycle.alternateCurrency?.symbol ?? ''
    : currentCycle.defaultCurrency?.symbol ?? ''
  |
  v
formatAmount(amount, currencySymbol) -- already accepts symbol param
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `components/MatrixControls.vue` | Modify | Replace exchange rate `<span>` with `<input type="number">`; add `localExchangeRate` ref; save on blur/Enter; readonly when all visible periods closed; sync `matrixStore.exchangeRate` after save |
| `components/MatrixCell.vue` | Modify | Replace `currencySymbol = ''` computed with lookup from `structureStore.currentCycle` based on `matrixStore.displayCurrency` |
| `components/MatrixSummaryRow.vue` | Modify | Add currency symbol to `formatAmount()` calls (pass symbol from same resolution as MatrixCell) |
| `components/MatrixTotalRow.vue` | Modify | Replace `totalBudgeted()`/`totalExecuted()` with consumption of `matrixStore.subtotalByLineType()`; add currency symbol |
| `store.ts` | Modify | Add `subtotalByLineType(periodId, lineType)` getter returning `{ budgeted, executed }`; add `syncExchangeRate()` action |
| `composables/useCurrencyDisplay.ts` | No change | Already correct; reads from `matrixStore.exchangeRate` |
| `i18n/locales/en.json` | Modify | Remove `budgetExecution.form.noteRequired` (line 270) and `budgetExecution.form.validation.noteRequired` (line 278) |
| `i18n/locales/es.json` | Modify | Remove same 2 keys (lines 270, 278) |
| `__tests__/ExecutionRecordForm.spec.ts` | Modify | Remove `noteRequired` fixture key (line 21); keep `noteRequiredAlways` (line 23) |
| `__tests__/ExecutionListModal.spec.ts` | Modify | Remove `noteRequired` fixture key (line 95) |

## Interfaces / Contracts

```typescript
// New getter in useBudgetMatrixStore (store.ts)
function subtotalByLineType(
  periodId: string,
  lineType: 'Expense' | 'LongTermSavings' | 'PreventiveSavings'
): { budgeted: number; executed: number }

// New action in useBudgetMatrixStore (store.ts)
async function syncExchangeRate(): Promise<void>
// Reads structureStore.currentCycle.exchangeRate and writes to matrixStore.exchangeRate
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `subtotalByLineType` getter returns correct sums per lineType | Pinia test with mock `periodTotals` + `budgetLines` |
| Unit | `MatrixCell` renders correct currency symbol per display mode | Component test with mocked stores |
| Unit | `MatrixControls` exchange rate input: visibility, readonly, save flow | Component test; verify `updateCycle` called with correct payload |
| Unit | `MatrixTotalRow` equals sum of 3 subtotals | Component test comparing against manual sum |
| Unit | S-002 removed keys do not appear in locale files | Snapshot or grep assertion in existing i18n test |

## Migration / Rollout

No migration required. All changes are frontend-only. No new API endpoints. Revert = revert commit.

## Open Questions

- [x] Does `structureStore.currentCycle` include `defaultCurrency.symbol`? -- Yes, `CycleListItem` has `defaultCurrency?: CurrencyItem` with `symbol: string`
- [x] Does `updateCycle()` update `currentCycle`? -- No, only updates `cycles[]` list; must re-fetch via `loadCycleDetail()`
- [ ] S-001: Does EF Core 10.x `Microsoft.EntityFrameworkCore.Sqlite` pull a non-vulnerable SQLitePCLRaw transitively? -- Verify at apply time with `dotnet list package --vulnerable --include-transitive`
