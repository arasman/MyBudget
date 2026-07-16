# Spec: budget-execution-ui-patch

**Change name**: `budget-execution-ui-patch`
**Type**: Delta on two existing capabilities
**Depends on**: `budget-execution` (archived), `budget-structure-ui` (archived)
**Date**: 2026-07-15

---

## Capability Index

| Capability | Type | Delta File |
|---|---|---|
| `budget-execution` | Modified (delta) | `specs/budget-execution/spec.md` |
| `budget-structure-ui` | Modified (delta) | `specs/budget-structure-ui/spec.md` |

---

## Requirements Summary

### budget-execution deltas

| ID | Change | Statement |
|---|---|---|
| REQ-EXEC-1 | Modified | `ExecutionRecord` MUST add nullable `OperationDate` (DateOnly) column |
| REQ-EXEC-LIST-2 | Modified | List response MUST include `operationDate` field per item |
| REQ-EXEC-FORM-1 | Added | `ExecutionRecordForm.vue` MUST expose OperationDate date picker, defaulting to today |
| REQ-EXEC-FORM-2 | Added | `ExecutionRecordForm.vue` MUST expose CurrencyId dropdown and ExchangeRate input |
| REQ-EXEC-CURRENCY-READ-1 | Added | `ListBudgetLines` response MUST include `currencyId` per line |

### budget-structure-ui deltas

| ID | Change | Statement |
|---|---|---|
| REQ-BL-2 | Modified | Inline add-line category dropdown MUST filter by the selected group |
| REQ-BL-3 | Modified | dblclick handlers in Matrix*Row components MUST call `window.getSelection()?.removeAllRanges()` |
| REQ-MATRIX-DND-1 | Added | Matrix rows MUST support drag-and-drop reorder regardless of period state |
| REQ-MATRIX-FOOTER-1 | Added | Footer MUST order Expenses → PreventiveSavings → LongTermSavings as "SubTotal" rows plus a Total row |
| REQ-MATRIX-RENDER-1 | Added | Name-only group/category edits MUST update incrementally without full matrix reload |

---

## Validation Error Codes (unchanged)

No new error codes. Existing `PERIOD_CLOSED`, `EXCHANGE_RATE_PAIR_INCOMPLETE`, and related codes continue to apply.

---

## Out of Scope

- Multi-currency matrix totals (`budget-execution-multicurrency`)
- New currency management UI
- Period close/open workflow changes
