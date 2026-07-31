# Archive Report: cut-record-totals-persistence

**Date**: 2026-07-30
**Change**: cut-record-totals-persistence
**Archive Location**: `openspec/changes/archive/2026-07-30-cut-record-totals-persistence/`
**Status**: Complete, archived with single resolved warning

---

## Executive Summary

The `cut-record-totals-persistence` SDD change has been successfully archived. All 19 tasks are complete and verified. The main spec (`openspec/specs/current-situation/spec.md`) has been updated with the delta spec, reflecting the shift from query-time to write-time totals computation and persisting all 16 total columns (8 concepts × primary/alternate). 

The verify phase reported PASS WITH WARNINGS, with one warning regarding the i18n key `currentSituation.totals.snapshotNotice` not being rendered by any Vue component. **This warning has been CLOSED by commit 8902804**, which added rendering of the key to CutTotalsPanel.vue. The user manually verified correct rendering in both locales and confirmed full E2E suite re-run (108 tests) all passing with no regressions.

---

## Artifacts Archived

### Moved to Archive

All change artifacts have been moved from `openspec/cut-record-totals-persistence/` to `openspec/changes/archive/2026-07-30-cut-record-totals-persistence/`:

- ✅ proposal.md — Scope, approach, risks, and success criteria
- ✅ spec.md — Delta spec with MODIFIED (CS-1, CS-2, CS-6) and ADDED (CS-9) requirements
- ✅ design.md — Technical approach, architecture decisions, testing strategy
- ✅ tasks.md — 19 tasks across 6 phases; all checked complete
- ✅ verify-report.md — Independent test verification with closure note on the WARNING
- ✅ archive-report.md — This file

### Engram Artifact IDs (for traceability)

| Artifact | Engram ID | Type |
|----------|-----------|------|
| Proposal | (not separately persisted; moved to openspec) | — |
| Spec (delta) | #391 | architecture |
| Design | #392 | architecture |
| Tasks | #393 | architecture |
| Verify Report | #395 | architecture |
| Apply Progress | #394 | architecture |
| Exploration | #389 | architecture |
| Archive Report | (this session, to be persisted) | architecture |

---

## Spec Merger Summary

### Main Spec Updated

`openspec/specs/current-situation/spec.md` — the authoritative specification for the `current-situation` capability — has been updated to reflect this change:

#### Modified Requirements
- **CS-1 (Upsert Cut Record)**: Added specification for server-side computation and persistence of all 16 total columns. Added 3 new scenarios:
  - "All 16 totals computed and persisted on save"
  - "Client-submitted totals ignored"
  - "Re-save overwrites all 16 totals"

- **CS-2 (Get Cut Record)**: Updated to specify that existing records return 16 persisted totals read directly from storage (no re-computation), while draft records still compute totals live. Added 1 new scenario:
  - "Draft computes all 8 total concepts live"

- **CS-6 (Cut Totals)**: Completely rewritten from "compute at query time" to "compute and persist at write time". Changed from 3 fields to 16 fields, added the full table of all 8 concepts × primary/alternate, documented snapshot semantics. Added 2 new scenarios:
  - "Totals computed correctly at save time"
  - "Snapshot unaffected by later data changes"
  - "Rounding precision"

#### Added Requirements
- **CS-9 (Migration Backfill for Persisted Totals)**: New requirement for the one-time migration that backfills all 16 columns for existing CutRecord rows. Includes 2 scenarios:
  - "Existing rows backfilled correctly"
  - "Columns non-nullable post-migration"

All other requirements (CS-3, CS-4, CS-5, CS-7, CS-8) remain unchanged; this change does not affect their specifications.

---

## Verification Summary

### Completeness
- ✅ All 19 tasks marked complete in tasks.md
- ✅ Phase 1 (Foundation): Entity, config, migration with 3-phase backfill
- ✅ Phase 2 (Shared components): CutTotalsCalculator, BudgetExecutionSummaryQuery extracted and shared
- ✅ Phase 3 (Handlers): Upsert and Get handlers reordered and updated per design
- ✅ Phase 4 (Backend tests): 6 unit tests + 23 integration test facts covering all spec scenarios
- ✅ Phase 5 (Frontend regression + E2E): cut-totals-snapshot.spec.ts E2E + regression check for DTO shape
- ✅ Phase 6 (Documentation): i18n keys for snapshot semantics added (now rendered as of commit 8902804)

### Test Results (Independent Verification)
- Backend: dotnet test 721 total / 718 passed / 3 skipped / 0 failed
- Frontend: 57 files / 442 tests / 0 failed
- E2E (post-fix): 108 tests / 108 passed / 0 failed

### Warning Resolved
**Original WARNING**: The i18n key `currentSituation.totals.snapshotNotice` was added to en.json and es.json (task 6.1) and had a parity test, but was not rendered by any Vue component, meaning the success criterion "reflected in ES + EN UI copy" was not literally satisfied.

**Resolution (commit 8902804)**: CutTotalsPanel.vue now includes a line rendering the key via `t('currentSituation.totals.snapshotNotice')` as a footnote explaining snapshot semantics. User manually verified rendering in both locales on the dev server. Full E2E re-run confirms no regressions (108 tests passing).

**Status**: ✅ CLOSED

---

## Source of Truth Updated

The main spec `openspec/specs/current-situation/spec.md` is now the authoritative source for the current-situation capability. It includes:
- All prior requirements (CS-1 through CS-8, with CS-1/CS-2/CS-6 modified)
- New requirement CS-9 for the migration backfill
- All updated scenarios and acceptance criteria reflecting the persisted-totals architecture

The delta spec in the archive (`openspec/changes/archive/2026-07-30-cut-record-totals-persistence/spec.md`) is retained for audit trail and historical reference only.

---

## SDD Cycle Closure

This change has completed all phases of the SDD workflow:
1. ✅ Proposal — Scope and approach confirmed
2. ✅ Spec — Delta spec created and reviewed
3. ✅ Design — Technical approach documented
4. ✅ Tasks — 19 tasks created, grouped into 6 phases
5. ✅ Apply — All tasks implemented and merged (feature-branch-chain: 3 PRs)
6. ✅ Verify — Independent test verification passed (with 1 resolved warning)
7. ✅ Archive — Change archived, spec merged, audit trail preserved

**Recommendation**: Ready for next phase. The change can be merged to main branch when user is ready.

---

## Rollback / Recovery

In the unlikely event rollback is needed:
- **Git**: Revert the 3-PR feature-branch-chain (commits 29e7751 through 8482391)
- **Database**: Run the migration's `Down()` method to drop the 16 persisted total columns
- **Spec**: Revert `openspec/specs/current-situation/spec.md` to restore original CS-1/CS-2/CS-6 definitions
- **Archive**: This archive directory remains for historical reference

No data loss — persisted totals are 100% recomputable from CutBankAccount rows and ExecutionRecords.

---

## Artifacts and Observation IDs

For traceability, all SDD artifacts associated with this change are documented:

### Engram (Architecture type, project: mybudget)
- obs-9a664c8d254c204c: #390 — Scope correction decision
- obs-b6a147f155cb9ee8: #391 — Spec (delta)
- obs-1bbc1d2bf6abc504: #392 — Design
- obs-b7ee9d50b5e37428: #393 — Tasks
- obs-6c4ebcd4d0665d13: #394 — Apply progress
- obs-c7a9b5155ef4b4fc: #395 — Verify report
- obs-f1276c3425bd1468: #389 — Exploration

### OpenSpec (File-based, archive location)
- openspec/changes/archive/2026-07-30-cut-record-totals-persistence/proposal.md
- openspec/changes/archive/2026-07-30-cut-record-totals-persistence/spec.md
- openspec/changes/archive/2026-07-30-cut-record-totals-persistence/design.md
- openspec/changes/archive/2026-07-30-cut-record-totals-persistence/tasks.md
- openspec/changes/archive/2026-07-30-cut-record-totals-persistence/verify-report.md
- openspec/changes/archive/2026-07-30-cut-record-totals-persistence/archive-report.md

### Main Spec (Updated)
- openspec/specs/current-situation/spec.md (merged delta, now canonical)

---

**Archive completed**: 2026-07-30 | Ready for user review and merge decision
