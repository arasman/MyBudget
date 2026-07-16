# Archive Report: budget-execution-ui-patch

**Change**: `budget-execution-ui-patch`
**Date**: 2026-07-15
**Artifact Store**: Hybrid (Engram + OpenSpec)
**Status**: ARCHIVED & COMPLETE

---

## Summary

The `budget-execution-ui-patch` change addresses all 8 deferred usability items from the `budget-execution-ui` capability: missing inline category selector, broken currency mapping, unwired drag-and-drop reorder, incorrect footer order/labeling, 3 unguarded STATUS_BREAKPOINT handlers, missing OperationDate field, and no Total row. The change shipped with 1 WARNING (mathematical divergence in MatrixTotalRow, no behavioral impact) and 2 SUGGESTIONS (pre-existing dependencies and stale i18n keys, for Phase-3 housekeeping).

**Verdict**: PASS — 0 CRITICAL, all 10 spec requirements satisfied, all 22 implementation tasks complete, all test suites green (166 Vitest + 421 .NET + 51 E2E).

---

## Artifacts

### Engram Source Records (for traceability)

| Topic Key | Observation ID | Type | Created | Status |
|-----------|----------------|------|---------|--------|
| sdd/budget-execution-ui-patch/explore | 222 | architecture | 2026-07-15 16:01:05 | active |
| sdd/budget-execution-ui-patch/proposal | 223 | architecture | 2026-07-15 16:02:11 | active |
| sdd/budget-execution-ui-patch/spec | 224 | architecture | 2026-07-15 16:25:34 | active |
| sdd/budget-execution-ui-patch/design | 225 | architecture | 2026-07-15 16:26:30 | active |
| sdd/budget-execution-ui-patch/tasks | 226 | architecture | 2026-07-15 16:31:19 | active |
| sdd/budget-execution-ui-patch/apply-progress | 227 | architecture | 2026-07-15 16:53:44 | active |
| sdd/budget-execution-ui-patch/verify-report | 228 | architecture | 2026-07-15 19:19:25 | active |

### OpenSpec Archive Location

```
openspec/changes/archive/2026-07-15-budget-execution-ui-patch/
├── explore.md
├── proposal.md
├── spec.md
├── design.md
├── tasks.md
├── apply-progress.md
├── verify-report.md
├── archive-report.md (this file)
└── specs/
    ├── budget-execution/spec.md
    └── budget-structure-ui/spec.md
```

---

## Specs Merged into Main Specs

### budget-execution (`openspec/specs/budget-execution/spec.md`)

**Delta merge**:
- MODIFIED: REQ-EXEC-1 (added `OperationDate` field) — updated entity requirement table + scenarios
- MODIFIED: REQ-EXEC-LIST-2 (added `operationDate` to response) — updated response field list + scenarios
- ADDED: REQ-EXEC-FORM-1 (OperationDate date picker) — appended to requirements table + scenarios
- ADDED: REQ-EXEC-FORM-2 (CurrencyId dropdown + ExchangeRate input) — appended + scenarios
- ADDED: REQ-EXEC-CURRENCY-READ-1 (CurrencyId in ListBudgetLines) — appended + scenarios

All other 43 requirements (REQ-EXEC-2 through REQ-EXEC-CASCADE-2, REQ-TOTALS-*, RST-6) preserved unchanged.

### budget-structure-ui (`openspec/specs/budget-structure-ui/spec.md`)

**Delta merge**:
- MODIFIED: REQ-BL-2 (inline category dropdown) — added filter scenarios to requirements + added condition to statement
- MODIFIED: REQ-BL-3 (text selection guard) — added `removeAllRanges()` scenarios + updated statement
- ADDED: REQ-MATRIX-DND-1 (DnD reorder for groups) — appended + 3 scenarios
- ADDED: REQ-MATRIX-FOOTER-1 (footer order, SubTotal labels, Total row) — appended + 3 scenarios
- ADDED: REQ-MATRIX-RENDER-1 (incremental render on name-only edits) — appended + 2 scenarios

All other 35 requirements (REQ-NAV-1 through REQ-CYC-5, REQ-PER-*, REQ-CAT-*, REQ-BL-1, REQ-I18N-1, REQ-FIX-*) preserved unchanged.

---

## Implementation Status

### All 22 Implementation Tasks Complete (Phases 1–6)

**Phase 1 — Backend Foundation** (4 tasks):
- ExecutionRecord.cs: DateOnly? OperationDate added
- Migration 20260715223718_AddOperationDateToExecutionRecord: nullable column added
- ListBudgetLinesQuery.cs: CurrencyId field added to BudgetLineResponse
- ListBudgetLinesHandler.cs: CurrencyId mapped in projection

**Phase 2 — Backend Command Layer** (2 tasks):
- CreateExecutionRecordCommand/Handler/Endpoint: OperationDate threaded through
- UpdateExecutionRecordCommand/Handler/Endpoint: OperationDate threaded through

**Phase 3 — Frontend Type Contracts** (2 tasks):
- budget-structure/types.ts: currency→currencyId rename; BudgetLineResponse.currencyId added
- budget-execution/types.ts: operationDate added to ExecutionRecordDto + request types

**Phase 4 — Frontend Component Patches** (5 tasks):
- BudgetLineModal.vue: currencyId from cycle Guids
- ExecutionRecordForm.vue: operationDate / currencyId / exchangeRate fields
- MatrixGroupRow.vue, MatrixCategoryRow.vue, MatrixLineRow.vue: removeAllRanges() on dblclick

**Phase 5 — Matrix View** (6 tasks):
- MatrixTotalRow.vue created (new component)
- BudgetMatrixView.vue footer: reordered, SubTotal labels, Total row appended
- i18n: en.json + es.json updated with summary + form keys
- BudgetMatrixView.vue inline category: `<select>` filtered by group
- BudgetMatrixView.vue DnD: group drag-and-drop wired (SortableJS, same endpoint as arrows)

**Phase 6 — Incremental Render** (1 task):
- In-place store updates confirmed (no-op: already implemented)

**Phase 7 — Manual Verification** (8 items, not code):
- User to verify currency Guid persistence, DnD reorder, footer layout, dblclick guard, OperationDate defaults, inline category filter, and incremental name-edit render.

---

## Verification: PASS

| Category | Result |
|----------|--------|
| CRITICAL Issues | 0 |
| WARNING Issues | 1 (W-001: MatrixTotalRow sums all lines instead of 3 subtotals — mathematically equivalent, no behavioral change) |
| SUGGESTION Issues | 2 (S-001: pre-existing SQLite vuln; S-002: stale i18n key — both for Phase-3 housekeeping) |
| Spec Requirements Satisfied | 10/10 (REQ-EXEC-1, -LIST-2, -FORM-1, -FORM-2, -CURRENCY-READ-1, REQ-BL-2, -3, -MATRIX-DND-1, -FOOTER-1, -RENDER-1) |
| Implementation Tasks Complete | 22/22 (Phases 1–6) |
| Build Status | Backend: clean ✅ | Frontend: clean ✅ |
| Test Suites | 166 Vitest ✅ | 284 .NET unit ✅ | 137 .NET integration ✅ | 51 E2E ✅ |

---

## Deferred to Phase 3

- **W-001 Accepted**: MatrixTotalRow mathematical divergence — low risk, no behavioral impact
- **S-001 Housekeeping**: Update SQLitePCLRaw dependency (pre-existing across 4 projects)
- **S-002 Housekeeping**: Prune stale `noteRequired` i18n key from en.json
- **Multi-currency totals**: Matrix display for non-default currency per-record conversion (requires backend query change)

---

## Archive Verification Checklist

- [x] Main specs synced with delta requirements (budget-execution, budget-structure-ui)
- [x] All implementation tasks marked complete by source inspection
- [x] No stale unchecked implementation tasks (Phases 1–6 all marked ✅; Phase 7 is manual verification by user)
- [x] Verify-report confirms PASS with no CRITICAL issues
- [x] All test suites green
- [x] Change folder moved to archive/2026-07-15-budget-execution-ui-patch/
- [x] Archive contains all artifacts: proposal, spec, design, tasks, apply-progress, verify-report, delta specs
- [x] All 10 spec requirements satisfied
- [x] No destructive merges

---

## SDD Cycle Complete

The `budget-execution-ui-patch` change has been fully:
1. Proposed (scope, approach, risks identified)
2. Specified (10 delta requirements across 2 capabilities)
3. Designed (technical decisions, data flows, file changes)
4. Tasked (6 phases, 22 implementation tasks + 1 verification phase)
5. Applied (all code implemented, tested, verified PASS)
6. Verified (0 CRITICAL, 1 WARNING accepted, all tests green)
7. **Archived** (delta specs merged into main specs, change folder moved to archive, audit trail persisted)

Ready for the next change.

---

## Observation IDs for Traceability

- Explore: #222
- Proposal: #223
- Spec: #224
- Design: #225
- Tasks: #226
- Apply Progress: #227
- Verify Report: #228
