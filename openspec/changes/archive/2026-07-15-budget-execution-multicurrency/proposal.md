# Proposal: Budget Execution Multi-Currency

## Intent

The budget matrix currently shows amounts only in the cycle's default currency, with exchange rate display as a read-only label and no currency symbol anywhere. Users managing dual-currency budgets (GTQ/USD) cannot toggle the matrix view between currencies or adjust the exchange rate inline. This change completes multi-currency display support, fixes MatrixTotalRow consistency, and cleans up two maintenance items.

## Scope

### In Scope

- **A: Multi-currency matrix display** -- make exchange rate editable in MatrixControls (save via existing PUT cycle endpoint); show real currency symbol in MatrixCell; all cells and footers convert using cycle exchange rate
- **W-001: MatrixTotalRow refactor** -- add store-level `subtotalByLineType(lineType, periodId)` getters; MatrixTotalRow sums from 3 lineType subtotals instead of raw line aggregation
- **S-001: SQLitePCLRaw verification** -- run `dotnet list package --vulnerable`; add explicit pin only if transitive version is still vulnerable
- **S-002: Stale i18n cleanup** -- remove `form.noteRequired` and `form.validation.noteRequired` from en.json/es.json; update 2 test stubs

### Out of Scope

- Per-record currency indicator in matrix cells (records show converted values only)
- New backend PATCH endpoint for exchange rate
- Pagination or filtering on execution lists
- Budget line currency editing from matrix view
- Multi-currency budgeted amounts (budgeted amounts are always in default currency)

## Capabilities

### New Capabilities

None

### Modified Capabilities

- `budget-execution`: add requirements for matrix currency display (editable exchange rate input, currency symbol rendering, display-only conversion rules)

## Approach

1. **Exchange rate input** (MatrixControls.vue): replace static `<span>` with editable numeric input; visible when alternate currency selected; read-only when all visible periods are closed; save calls `structureStore.updateCycle()` with re-fetched cycle data to avoid stale snapshot
2. **Currency symbol** (MatrixCell.vue): read `defaultCurrency`/`alternateCurrency` from `structureStore.currentCycle` based on `displayCurrency` mode; pass symbol to `formatAmount()`
3. **W-001** (store.ts + MatrixTotalRow.vue): add `subtotalByLineType(lineType, periodId)` computed getters returning `{ budgeted, executed }` per lineType; MatrixTotalRow consumes these instead of direct line sums
4. **S-001**: verify with `dotnet list package --vulnerable`; pin `SQLitePCLRaw.lib.e_sqlite3` in 2 csproj files only if needed
5. **S-002**: delete 2 keys from both locale files; update test fixtures in `ExecutionRecordForm.spec.ts` and `ExecutionListModal.spec.ts`

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/.../MatrixControls.vue` | Modified | Editable exchange rate input + save |
| `frontend/.../MatrixCell.vue` | Modified | Wire currency symbol from store |
| `frontend/.../MatrixTotalRow.vue` | Modified | W-001: consume store subtotals |
| `frontend/.../store.ts` | Modified | Add subtotalByLineType getters |
| `frontend/.../i18n/locales/en.json` | Modified | Remove 2 stale keys |
| `frontend/.../i18n/locales/es.json` | Modified | Remove 2 stale keys |
| `frontend/.../__tests__/ExecutionRecordForm.spec.ts` | Modified | Update fixture for removed key |
| `frontend/.../__tests__/ExecutionListModal.spec.ts` | Modified | Update fixture for removed key |
| `*.csproj` (2 test projects) | Conditional | S-001 pin if vulnerable |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Exchange rate direction ambiguity (`/ rate` vs `* rate`) | Med | Preserve existing `convert()` formula; add unit test asserting GTQ->USD conversion |
| Stale cycle snapshot on rate save overwrites other fields | Med | Re-fetch `loadCycleDetail()` before calling `updateCycle()` |
| S-001 scope mismatch (roadmap says 4 projects, repo has 2) | Low | Verify with `dotnet list package --vulnerable` before writing tasks |
| W-001 double-counting if lineType category sets overlap | Med | Unit test verifying union of 3 subtotals equals full total |

## Rollback Plan

All changes are frontend-only (no DB migrations, no new API endpoints). Revert the commit to restore previous behavior. S-001 pin (if added) is a dependency version bump with no code change -- revert the csproj line.

## Dependencies

- `budget-execution-ui-patch` archived (2026-07-15) -- prerequisite complete
- Existing `structureStore.updateCycle()` and `useCurrencyDisplay.convert()` -- reused as-is

## Success Criteria

- [ ] Currency toggle switches all matrix cell values and footer totals between GTQ and USD
- [ ] Currency symbol (e.g. "Q", "$") displays next to amounts in matrix cells
- [ ] Exchange rate is editable inline when alternate currency selected and at least one period is open
- [ ] Exchange rate save persists via PUT cycle and refreshes matrix values
- [ ] MatrixTotalRow values equal sum of 3 MatrixSummaryRow subtotals (no direct line aggregation)
- [ ] `dotnet list package --vulnerable` returns clean (or explicit pin resolves it)
- [ ] No stale i18n keys remain; all tests pass after fixture update
