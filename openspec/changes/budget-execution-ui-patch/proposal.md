# Proposal: budget-execution-ui-patch

## Intent

The budget execution matrix UI shipped with 8 deferred items that degrade usability: missing inline category selector, broken currency mapping, unwired drag-and-drop reorder, incorrect footer order/labeling, 3 unguarded STATUS_BREAKPOINT handlers, missing OperationDate field, and no Total row. This patch addresses all items except multi-currency totals (Phase 3).

## Scope

### In Scope
- **Category selector in inline add-line**: dropdown filtered by parent group's categories
- **Currency bug fix**: map `currency` string to `CurrencyId` Guid; add `currencyId` to read model
- **DnD reorder**: wire `vue-draggable-plus` for groups/categories/lines alongside existing arrow buttons; reorder enabled regardless of period state (structural, not period-scoped)
- **Summary footer**: reorder to Expenses > PreventiveSavings > LongTermSavings, rename labels to "SubTotal", add Total row (sum of 3 subtotals)
- **STATUS_BREAKPOINT guards**: fix 3 remaining handlers (MatrixGroupRow, MatrixCategoryRow, MatrixLineRow)
- **OperationDate field**: add nullable `DateOnly` column + migration to `ExecutionRecord`; expose in form with default=today, editable
- **ExecutionRecord form — currency/exchange rate**: expose existing `CurrencyId` and `ExchangeRate` fields (already on entity) in `ExecutionRecordForm.vue`; no new migration needed
- **Render optimization**: incremental refresh for name-only group/category edits

### Out of Scope
- Multi-currency matrix totals display (→ separate `budget-execution-multicurrency` spec)
- New currency management UI
- Period close/open workflow changes

## Capabilities

### New Capabilities
- None

### Modified Capabilities
- `budget-execution`: add `OperationDate` field (REQ-EXEC-1 entity change + new requirement)
- `budget-structure-ui`: DnD reorder wiring, inline category selector, footer layout, STATUS_BREAKPOINT fixes, render optimization

## Approach

Two-phase delivery within one change:
1. **Frontend patch** (Phase 1): DnD, footer, STATUS_BREAKPOINT, currency fix, category selector, render optimization -- pure frontend except currency read-model fix
2. **OperationDate + form fields** (Phase 2): EF migration (nullable DateOnly, safe additive), command/query handler update, form exposes OperationDate (default today) + existing CurrencyId/ExchangeRate fields

### Key decisions from user answers
- Category dropdown filters to categories within the current group (preserves hierarchy)
- DnD/reorder stays enabled on closed periods (structural preference, not period-scoped write)
- OperationDate = real-world transaction date; defaults to today, editable
- Total row = sum of all 3 SubTotals

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/src/features/budget-structure/components/BudgetMatrixView.vue` | Modified | Inline category selector, footer reorder/Total row |
| `frontend/src/features/budget-structure/components/Matrix*Row.vue` | Modified | DnD + STATUS_BREAKPOINT fixes |
| `frontend/src/features/budget-structure/components/BudgetLineModal.vue` | Modified | Currency mapping fix |
| `frontend/src/features/budget-structure/types.ts` | Modified | Currency type alignment |
| `frontend/src/features/budget-execution/components/ExecutionRecordForm.vue` | Modified | OperationDate field |
| `backend/.../ExecutionRecord.cs` | Modified | OperationDate property |
| `backend/.../Migrations/` | New | AddOperationDate migration |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| DnD conflicts with arrow-button reorder state | Low | Both use same backend reorder endpoint; UI disables one while other is active |
| Currency fix breaks existing records with null CurrencyId | Low | Migration is additive; existing nulls fall back to cycle default (current behavior) |
| OperationDate migration on populated DB | Low | Nullable column, no default constraint needed |

## Rollback Plan

All changes are additive. Frontend reverts via git. OperationDate migration has a generated `Down()` that drops the column. No data loss path.

## Dependencies

- `vue-draggable-plus@0.6.1` already installed
- Backend reorder endpoints already exist

## Success Criteria

- [ ] Inline add-line shows category dropdown filtered by parent group
- [ ] Currency saves correctly as Guid; edit form pre-populates currency
- [ ] Groups/categories/lines draggable; reorder persists; works on closed periods
- [ ] Footer shows Expenses > PreventiveSavings > LongTermSavings as SubTotals + Total
- [ ] STATUS_BREAKPOINT no longer reachable in 3 matrix row components
- [ ] OperationDate appears in execution form, defaults to today, saves/reads correctly
- [ ] CurrencyId and ExchangeRate appear in execution form, save/read correctly
- [ ] Name-only edits refresh incrementally (no full matrix reload)
