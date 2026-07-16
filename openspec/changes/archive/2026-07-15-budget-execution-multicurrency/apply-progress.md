# Apply Progress: Budget Execution Multi-Currency

**Status**: complete (all 7 phases done)
**Mode**: Standard (Strict TDD: OFF)
**Test result**: 27 test files, 186 tests — all passing

---

## Phase 1 — Store Foundation [DONE]

- Added `subtotalByLineType(periodId, lineType)` getter in `store.ts`.
- Added `syncExchangeRate()` action.
- Updated `useBudgetMatrixStore.spec.ts` with 7 new tests.

## Phase 2 — MatrixTotalRow Refactor (W-001) [DONE]

- Replaced raw aggregation with 3 calls to `subtotalByLineType`.
- Added `currencySymbol` computed.
- Created `MatrixTotalRow.spec.ts` (3 tests).

## Phase 3 — MatrixCell Currency Symbol [DONE]

- Replaced `currencySymbol = ''` with computed from `structureStore.currentCycle`.
- Updated `MatrixCell.spec.ts` (3 new tests).

## Phase 4 — MatrixSummaryRow Currency Symbol [DONE]

- Added `currencySymbol` computed; passed to `formatAmount()`.
- Updated `MatrixSummaryRow.spec.ts` (1 new test + updated existing assertions).

## Phase 5 — MatrixControls Exchange Rate Input [DONE]

- Added `localExchangeRate` ref + editable `<input type="number">`.
- Implemented `saveExchangeRate()` with double `loadCycleDetail` pattern.
- Created `MatrixControls.spec.ts` (7 tests).

## Phase 6 — i18n Cleanup (S-002) [DONE]

- Removed `noteRequired` and `validation.noteRequired` from `en.json` and `es.json`.
- Updated `ExecutionRecordForm.spec.ts` and `ExecutionListModal.spec.ts` fixtures.

## Phase 7 — SQLitePCLRaw Vulnerability (S-001) [DONE]

- Vulnerability found in 4 projects; pinned `SQLitePCLRaw.lib.e_sqlite3` to `3.53.3` in all 4.
- `dotnet list package --vulnerable` clean for all projects.

---

## Deviations

1. SQLitePCLRaw pin applied to 4 projects (design said 2; all 4 had the vulnerability).
2. `syncExchangeRate` implemented as sync (no awaitable work needed).
3. `allPeriodsClosed` uses `matrixStore.allPeriods` (not just visible window) — conservative choice.
