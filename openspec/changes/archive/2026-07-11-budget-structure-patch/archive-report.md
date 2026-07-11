# Archive Report: budget-structure-patch

**Change**: `budget-structure-patch`
**Status**: ARCHIVED
**Date**: 2026-07-11
**Archive path**: `openspec/changes/archive/2026-07-11-budget-structure-patch/`

---

## SDD Cycle Summary

The `budget-structure-patch` SDD change has been fully completed: designed, implemented across 3 chained PRs, verified (PASS WITH WARNINGS, 0 CRITICAL), and archived.

**Verdict**: READY FOR ARCHIVE

---

## Artifact Observations (Engram IDs for Traceability)

All change artifacts were persisted to Engram during their respective SDD phases:

| Artifact | Engram ID | Topic Key | Type | Summary |
|---|---|---|---|---|
| Proposal | #155 | sdd/budget-structure-patch/proposal | architecture | Scope: Currency table, Cycle currency fields, BudgetLineRevision currency migration, BudgetLine DisplayOrder, Restore endpoints (5 items) |
| Spec | #157 | sdd/budget-structure-patch/spec | architecture | Formal delta spec; 5 capabilities (currency-reference, cycle-currency, budget-line-currency, budget-line-display-order, budget-restore); 30 requirements (CUR-1..4, CYC-1..9, BLR-1..5, BLD-1..3, RST-1..7) |
| Design | #159 | sdd/budget-structure-patch/design | architecture | Technical design: Currency entity (seeded, no soft-delete), Cycle fields (ExchangeRate precision), Restore pattern, 25 files changed, PR split strategy (3 chained PRs) |
| Tasks | #160 | sdd/budget-structure-patch/tasks | architecture | Task checklist: 24 main tasks across 3 PRs (PR1 Currency+Cycle, PR2 BudgetLine+DisplayOrder, PR3 Restore); 66 subtasks total |
| Verify Report | #162 | sdd/budget-structure-patch/verify-report | architecture | Verification: PASS WITH WARNINGS; 218 tests passed, 0 failed; 66/66 tasks complete; 0 CRITICAL, 3 intentional WARNING deviations |

---

## Spec Integration

The delta spec has been merged into the main `budget-structure/spec.md`:

**Main spec location**: `D:/Projects/bigschool/TFM/MyBudget/openspec/specs/budget-structure/spec.md`

**Changes merged**:
- Added **Capability Index** listing 8 capabilities (6 core + 2 patch-added)
- Added **Section 6: Currency Reference** (REQ-CUR-01, REQ-CUR-02) — Currency entity and read-only catalog endpoint
- Added **Section 7: Cycle Currency Fields** (REQ-CYC-CUR-01, REQ-CYC-CUR-02) — DefaultCurrencyId, AlternateCurrencyId, ExchangeRate, response projections
- Extended **Section 5 (BudgetLines)**: REQ-BL-05 covers CurrencyId and DisplayOrder for BudgetLine
- Added **Section 8: Restore Endpoints** (REQ-RST-01 through REQ-RST-06) — Restore() methods, cascade patterns, parent-deleted guards, includeExecutionRecords parameter
- Renumbered **Section 9: Read Endpoints** (was Section 6)

**Validation rules updated**: Added CYC_PAIR_INCOMPLETE, PARENT_IS_DELETED, REORDER_ID_NOT_IN_SCOPE, REORDER_DUPLICATE_ID.

---

## Implementation Summary

**Delivery**: 3 chained feature-branch PRs (main → feat/budget-patch-currency → feat/budget-patch-budgetline → feat/budget-patch-restore)

**Lines of code**: ~900–1100 changed across 3 PRs (per design forecast)

**Test coverage**: 218 tests (unit + integration) — all passing

**Task completion**: 66/66 tasks marked complete; verified by apply-progress and verify-report

| PR Slice | Focus | Status |
|---|---|---|
| PR1 | Currency entity + Cycle fields + ListCurrencies + Cycle CRUD updates | ✅ Merged |
| PR2 | BudgetLineRevision currency FK + BudgetLine DisplayOrder + ReorderBudgetLines | ✅ Merged |
| PR3 | Restore() methods on 5 entities + 4 restore cascade endpoints | ✅ Merged |

---

## Verification Summary

**Verdict**: PASS WITH WARNINGS (0 CRITICAL, 3 pre-documented intentional deviations)

**Test Evidence**:
- Backend unit/integration: 218 passed, 0 failed
- Build: SUCCESS

**Spec Compliance**:
- CUR-1 through CUR-4: PASS (Currency entity, seeds, no soft-delete, GET endpoint)
- CYC-1 through CYC-9: PASS (all currency fields, pair rule, response projections)
- BLR-1 through BLR-4: PASS (CurrencyId FK, migration DELETE, handler resolution)
- BLR-5: WARNING (flat response shape vs nested spec — intentional, no frontend breakage)
- BLD-1 through BLD-3: PASS (DisplayOrder, backfill, ReorderBudgetLines endpoint)
- RST-1 through RST-7: PASS with WARNINGS (RST-4 and RST-5 routes deviate from spec examples, consistent with existing DELETE patterns — intentional, documented)

**Warnings (intentional, documented)**:
1. **WARN-001** (BLR-5): API response uses flat `currencyCode`/`currencySymbol` instead of nested `currency:{code,symbol}`. No frontend impact.
2. **WARN-002** (RST-4): RestoreCategory uses nested route `/category-groups/{groupId}/categories/{categoryId}/restore` per existing DELETE pattern, not flat spec example.
3. **WARN-003** (RST-5): RestoreBudgetLine uses `lines/` segment per existing DELETE pattern, not `budget-lines/` from spec example.

**All warnings pre-documented in verify-report.md and approved for archive**.

---

## Archive Contents

The following files have been moved to `openspec/changes/archive/2026-07-11-budget-structure-patch/`:

- `proposal.md` — original proposal
- `spec.md` — delta spec
- `design.md` — technical design
- `tasks.md` — task checklist (66/66 complete)
- `verify-report.md` — verification verdict and findings
- `archive-report.md` — this archive report

---

## ROADMAP Update

`openspec/ROADMAP.md` section 5 (`budget-structure-patch`) has been updated:
- Status changed from 🔄 in progress → ✅ archived 2026-07-11
- Added completion note: 66 implementation tasks, 218 tests passing, PASS WITH WARNINGS
- SDD artifacts path updated: `openspec/archive/2026-07-11-budget-structure-patch/`

---

## Next Steps

The `budget-structure-patch` change unblocks:
- **`budget-execution`**: can now use Currency fields and Restore endpoints as forward-compatible foundation
- **`audit-log`**: can now audit all structural entity mutations including Restore operations
- **Frontend patches**: no UI changes in this scope; existing frontend remains compatible

The feature branch `feat/budget-structure-patch` has been merged to `main` and can be deleted.

---

## Archive Checklist

- [x] All artifact observation IDs recorded (Engram #155, #157, #159, #160, #162)
- [x] Delta specs merged into main specs (budget-structure/spec.md)
- [x] No unchecked implementation tasks remaining (66/66 complete)
- [x] No CRITICAL verification issues (0 CRITICAL, 3 intentional WARNINGS)
- [x] Change folder ready for archive move
- [x] ROADMAP.md updated with archive status
- [x] Archive report persisted to Engram (sdd/budget-structure-patch/archive-report)

**Status**: ARCHIVE READY ✅
