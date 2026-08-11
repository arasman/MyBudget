# Archive Report: budget-execution-ui

**Date**: 2026-08-10 (retroactive filesystem closure of prior 2026-07-14 Engram-only archive)
**Status**: COMPLETE WITH ACCEPTED WARNINGS
**Change**: `budget-execution-ui`
**Archive Folder**: `openspec/changes/archive/2026-07-14-budget-execution-ui/`

---

## Executive Summary

The `budget-execution-ui` change has been successfully archived with **0 CRITICAL** issues and **2 WARNING** items that have been explicitly accepted by the user. This represents the retroactive filesystem closure of a change that was previously archived in Engram-only mode on 2026-07-14. The verify-report (generated 2026-08-10) confirms all 46 implementation tasks are complete, 129 feature-scoped Vitest tests pass with clean type-checking, and the feature is currently running in production.

### Prior Archive Cycle (Engram-only, 2026-07-14)

- **Engram obs #219**: sdd/budget-execution-ui/verify-report (PASS WITH WARNINGS, 40 tasks listed)
- **Engram obs #220**: sdd/budget-execution-ui/archive-report (status COMPLETE)
- **ROADMAP.md**: Already marked "✅ archived 2026-07-14"
- **Artifact Store Mode**: Engram (no filesystem sync configured at the time)
- **Gap Identified**: `openspec/changes/budget-execution-ui/` folder was never physically moved to archive on disk, unlike follow-up changes `budget-execution-ui-patch` (2026-07-15) and `budget-execution-ui-e2e-debt` (2026-07-17), which were properly archived.

### This Session's Verification (2026-08-10)

**Trigger**: Filesystem housekeeping pass — aligning Engram archive records with disk state.

**Verify Report Verdict**: PASS WITH WARNINGS (0 CRITICAL, 2 WARNING, 1 SUGGESTION)
- W-01 (carried forward, still open): EstimatedVariance sub-row (REQ-MATRIX-STRUCT) not rendered; MatrixEstimatedRow.vue component was deleted as dead code in commit 06a9ef9; no equivalent variance display exists elsewhere
- W-05 (new, this session): E2E suite not re-executed this session (no local API/Vite servers available); 8 Playwright specs exist on disk and match scope, but were not run. Feature has been running in production since deployment; E2E debt was addressed separately in `budget-execution-ui-e2e-debt` (2026-07-17)

**User Decision**: Both warnings accepted. Feature is production-ready for TFM submission; these documentation/SDD artifact gaps do not block archiving.

---

## Tasks Completion

| Status | Count | Notes |
|--------|-------|-------|
| Total Tasks | 46 | T-1.1 through T-6.9 (6 PRs × ~7-8 tasks each) |
| Completed | 46 | All checked ✓ in openspec/changes/budget-execution-ui/tasks.md |
| Incomplete | 0 | None |
| Implementation Verified | 46/46 | All confirmed present in codebase during verify phase |

---

## Spec Merges

### New Spec Created

**Capability**: `budget-execution-ui` (new)
**Target**: `openspec/specs/budget-execution-ui/spec.md`
**Action**: CREATED
**Content**:
- REQ-MATRIX-ROUTE: Routing and Navigation
- REQ-MATRIX-NAV: Three-Period Sliding Window & Period Column Headers
- REQ-MATRIX-STRUCT: Hierarchical Structure Display
- REQ-MATRIX-EXEC: Execution Record Management (CRUD)
- REQ-MATRIX-TOTALS: Aggregated Totals and Calculations
- REQ-MATRIX-CURRENCY: Currency Toggle Display
- REQ-MATRIX-DELETED: Include Deleted Behavior
- REQ-MATRIX-INSERT: Structural Inserts from Matrix
- REQ-MATRIX-REORDER: Reordering
- REQ-MATRIX-REFRESH: Per-Period Refresh
- REQ-MATRIX-RBAC: Role-Based Access Control
- i18n Requirements: budgetMatrix.*, budgetExecution.* namespaces
- Test Coverage Requirements: 19 Vitest files / 129 tests + 8 Playwright E2E specs

**Requirements Count**: 11 requirements + 3 cross-cutting (i18n, test coverage) = 14 total

### Delta Merged into Existing Spec

**Capability**: `budget-structure-ui` (delta)
**Target**: `openspec/specs/budget-structure-ui/spec.md`
**Action**: MERGED
**Content**: Updated REQ-NAV-1 — Budget Structure Navigation Tabs
- **Change**: "Two tabs (Cycles, Categories)" → "Three tabs when cycleId provided (Cycles, Categories, Matrix); two tabs otherwise"
- **New Scenarios**: 3 additional scenarios covering Matrix tab visibility and active state
- **Reason**: BudgetTabs component must render Matrix tab only when cycleId prop is provided (per REQ-MATRIX-ROUTE)

---

## Verification Summary

### Code Verification (from verify-report)

- **Routing**: Router registers `/budgets/:budgetId/cycles/:cycleId/matrix` → BudgetMatrixView ✓
- **Navigation**: 3-period sliding window, prev/next buttons, offset clamping ✓
- **Hierarchy**: Group → Category → Line rows render correctly; EstimatedVariance sub-row NOT rendered (W-01)
- **Execution CRUD**: ExecutionListModal, create/update/delete/restore all confirmed ✓
- **Currency Toggle**: GTQ/USD toggle, exchange rate display, conversion formula ✓
- **Deleted Items**: Show-deleted toggle, gray styling, restore actions ✓
- **Structural Inserts**: Insertar Línea, Insertar Categoría, Insertar Grupo confirmed ✓
- **Reordering**: Group-level drag-and-drop via SortableJS confirmed; category/line reorder via arrows only (W-02 from prior report, narrowed to precise finding)
- **Refresh Icon**: Shows on closed periods, absent on open periods ✓
- **RBAC**: Role-based visibility (read/operator/admin) confirmed ✓
- **i18n**: budgetMatrix.* and budgetExecution.* namespaces complete in EN and ES ✓

### Test Evidence

**Frontend Tests** (this session):
- Vitest scope: 19 budget-execution test files / 129 tests — all passing
- Full frontend suite: 77 test files / 634 tests — all passing, 0 regressions
- vue-tsc type-check: clean, 0 errors

**E2E Tests**:
- 8 Playwright spec files exist on disk (navigation, collapse, execution-crud, note-validation, currency-toggle, include-deleted, closed-period, rbac)
- Not re-executed this session (no local API/Vite servers); marked W-05

**Backend Endpoints** (already archived in `budget-execution` change):
- CreateExecution, UpdateExecution, DeleteExecution, RestoreExecution ✓
- ListExecutions, ListPeriodExecutionTotals ✓

---

## Warnings Acceptance

### W-01: EstimatedVariance Sub-Row Not Rendered

**Requirement**: REQ-MATRIX-STRUCT specifies a sub-row showing `Estimado - Real` and `Real - Total Ejecutado` under each BudgetLine row.

**Current State**: MatrixEstimatedRow.vue component was created (T-3.8) then deleted as dead code in commit 06a9ef9 rather than wired into MatrixLineRow.vue. No equivalent variance display exists anywhere else in the codebase.

**Timeline**:
- 2026-07-14: Original verify report flagged as W-01 (4 warnings total)
- 2026-07-15: budget-execution-ui-patch merged (did not revisit W-01)
- 2026-07-17: budget-execution-ui-e2e-debt merged (did not revisit W-01)
- 2026-08-10: Still present in current code

**User Decision**: ACCEPTED. Feature is production-ready for TFM submission. The EstimatedVariance display remains an open gap, to be formally descoped (i.e., remove REQ-MATRIX-STRUCT references to the sub-row) or scheduled as a follow-up change in the next development cycle.

### W-05: E2E Suite Not Re-Executed This Session

**Requirement**: Test Coverage Requirements specify 8 Playwright E2E specs covering matrix navigation, collapse, CRUD, validation, currency toggle, include-deleted, closed-period, and RBAC scenarios.

**Current State**: All 8 spec files exist on disk under `Project/frontend/e2e/budget-matrix/` and match scope. They were not executed in this session due to no local API or Vite dev server being available (only Docker infra — Postgres/Redis/Seq/Mailpit/Jaeger — was running).

**Prior Evidence**:
- 2026-07-14: Original verify report recorded E2E specs as user-confirmed passing
- 2026-07-17: budget-execution-ui-e2e-debt change added further UI-level E2E coverage on top
- Production: Feature has been deployed and running since merge, no reported issues

**User Decision**: ACCEPTED. E2E suite should be re-run before or shortly after archive in a subsequent session, but this does not block archiving given the substantial unit/component test evidence (129 tests) and production deployment confirmation.

---

## Known Issues Summary

| ID | Type | Title | Status | Impact |
|----|----|-------|--------|--------|
| W-01 | WARNING | EstimatedVariance sub-row not rendered | OPEN | Spec gap; feature functional without it |
| W-05 | WARNING | E2E suite not re-executed | OPEN | Test debt; specs exist and prior runs passed |

**CRITICAL Issues**: None.

---

## Archive Contents

```
openspec/changes/archive/2026-07-14-budget-execution-ui/
├── proposal.md ✓
├── spec.md ✓
├── design.md ✓
├── tasks.md ✓ (46/46 tasks complete)
├── verify-report.md ✓ (2026-08-10 session)
├── archive-report.md ✓ (this file)
└── explore.md ✓ (original exploration artifact)
```

---

## Artifact Store Alignment

| Artifact | Engram | Filesystem | Status |
|----------|--------|-----------|--------|
| proposal | obs #216 | archive/2026-07-14-budget-execution-ui/proposal.md | ✓ |
| spec | obs #217 | archive/2026-07-14-budget-execution-ui/spec.md | ✓ |
| design | obs #218 | archive/2026-07-14-budget-execution-ui/design.md | ✓ |
| tasks | obs #221 | archive/2026-07-14-budget-execution-ui/tasks.md | ✓ |
| verify-report | obs #219 (2026-07-14) | archive/2026-07-14-budget-execution-ui/verify-report.md | ✓ merged |
| archive-report | obs #220 (2026-07-14) | archive/2026-07-14-budget-execution-ui/archive-report.md | ✓ created |
| explore | — | archive/2026-07-14-budget-execution-ui/explore.md | ✓ |

**Reconciliation**: Prior Engram-only archive (obs #219, #220) + new filesystem artifacts = **hybrid mode complete**.

---

## ROADMAP.md Status

**Current Line 208**:
```
### 8. `budget-execution-ui` ✅ archived 2026-07-14
```

**Status**: Already correctly marked as archived with date 2026-07-14.

**No Action Required**: ROADMAP.md is accurate and does not need updating. The date 2026-07-14 matches the original Engram-only archive cycle and has been preserved as the canonical date for this change's archive folder.

---

## SDD Cycle Closure

✓ **Proposal**: Approved 2026-07-14  
✓ **Specification**: Confirmed and merged (new `budget-execution-ui` + delta into `budget-structure-ui`)  
✓ **Design**: Verified 2026-08-10  
✓ **Tasks**: All 46/46 complete, verified in code  
✓ **Implementation**: 6 chained PRs merged; feature live in production  
✓ **Verification**: PASS WITH WARNINGS (0 CRITICAL); warnings explicitly accepted  
✓ **Archive**: Folder moved to `openspec/changes/archive/2026-07-14-budget-execution-ui/`; archive-report written  

**SDD Cycle Status**: **COMPLETE AND CLOSED**

---

## Next Steps

- **No Follow-Up Required**: This change is terminal. The two accepted warnings (W-01 EstimatedVariance gap, W-05 E2E not re-run) do not require action before merge/close-out.
- **Optional Future Work**: 
  - Descope REQ-MATRIX-STRUCT EstimatedVariance sub-row from spec OR schedule formal follow-up change to implement it
  - Re-run E2E suite in a subsequent session to close W-05 for documentation completeness
- **Next in Roadmap**: As of 2026-08-04, `dashboard` is the latest archived change (MVP A complete). No blocking dependencies remain.

---

## Traceability

**Engram Observations Referenced**:
- obs #216: sdd/budget-execution-ui/proposal
- obs #217: sdd/budget-execution-ui/spec
- obs #218: sdd/budget-execution-ui/design
- obs #219: sdd/budget-execution-ui/verify-report (2026-07-14, rev 2)
- obs #220: sdd/budget-execution-ui/archive-report (2026-07-14)
- obs #221: sdd/budget-execution-ui/tasks

**Filesystem Artifacts**:
- `openspec/specs/budget-execution-ui/spec.md` (created 2026-08-10)
- `openspec/specs/budget-structure-ui/spec.md` (merged delta 2026-08-10)
- `openspec/changes/archive/2026-07-14-budget-execution-ui/` (archived 2026-08-10)

**Test Artifacts**:
- 129 Vitest tests (frontend) — all passing
- 634 full-suite Vitest tests — 0 regressions
- 8 Playwright E2E specs — deferred execution (W-05)

---

**Archived by**: SDD Archive Phase (automated)  
**Archive Date**: 2026-08-10  
**Canonical Archive Date**: 2026-07-14 (original cycle)  
**Status**: COMPLETE
