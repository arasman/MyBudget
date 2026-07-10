# Verification Report: budget-structure

**Change**: budget-structure | **Mode**: Hybrid | **Date**: 2026-07-09 | **Strict TDD**: ON

**Verdict**: PASS WITH WARNINGS

---

## Completeness Summary

| Dimension | Count | Status |
|-----------|-------|--------|
| Tasks complete | 58/58 | All checked |
| Slice folders | 23/23 | All present |
| Write slices 4-file | 19/19 | Command + Validator + Handler + Endpoint |
| Read slices 3-file | 4/4 | Query + Handler + Endpoint |
| Entities | 6 + LineType enum | All present |
| EF configurations | 6 | All present |
| Migration | 1 | AddBudgetStructureTables |
| Build errors | 0 | Clean |
| Unit tests passing | 170/170 | All pass (live run) |
| Integration tests | 90 | All pass (user-confirmed) |
| Total tests | 260 | 0 failures |

---

## Build Evidence

Build succeeded. 0 Error(s), 8 Warning(s) - all NU1903 SQLitePCLRaw pre-existing transitive dependency.
Unit test run (live): Passed! Failed:0, Passed:170, Skipped:0, Total:170

---

## RBAC Verification

All 19 write endpoints: .RequireAuthorization(budget:admin)
All 4 read endpoints: .RequireAuthorization(budget:read)
REQ-SC-02: COMPLIANT.

---

## Spec Compliance Matrix

### REQ-SC-* Shared Constraints

| Requirement | Covered By | Status |
|-------------|------------|--------|
| REQ-SC-01 Auth | CycleTests.CreateCycle_Unauthenticated_Returns401 | PASS |
| REQ-SC-02 RBAC | CycleTests.CreateCycle_ViewerRole_Returns403 | PASS |
| REQ-SC-03 Budget isolation | ResourceIsolationTests (4 tests) | PASS |
| REQ-SC-04 Soft delete | Cascade tests Cycle/Period/CategoryGroup/Category | PASS |
| REQ-SC-05 Hard delete order | Migration Down() order verified | PASS |

### REQ-CYC-* Cycles

| Requirement | Scenarios covered | Status |
|-------------|-------------------|--------|
| REQ-CYC-01 Create | Happy path + date overlap + StartDate after EndDate | PASS |
| REQ-CYC-02 Update | Happy path + period-out-of-range | PASS |
| REQ-CYC-03 Delete | Cascade soft-delete | PASS |
| REQ-CYC-04 SetActive | Atomic swap + no-prior-active | PASS |

### REQ-PER-* Periods

| Requirement | Scenarios covered | Status |
|-------------|-------------------|--------|
| REQ-PER-01 Create | Happy path + outside-range + overlap | PASS |
| REQ-PER-02 Update | Happy path | PASS |
| REQ-PER-03 SetStatus | Close + reopen | PASS |
| REQ-PER-04 Delete | Cascade soft-delete | PASS |

### REQ-CG-* CategoryGroups

| Requirement | Scenarios covered | Status |
|-------------|-------------------|--------|
| REQ-CG-01 Create | Happy path + duplicate name | PASS |
| REQ-CG-02 Update | Happy path | PASS |
| REQ-CG-03 Delete | Cascade soft-delete | PASS |
| REQ-CG-04 Reorder | Happy path + incomplete list | PASS |

### REQ-CAT-* Categories

| Requirement | Scenarios covered | Status |
|-------------|-------------------|--------|
| REQ-CAT-01 Create | Happy path + duplicate name | PASS |
| REQ-CAT-02 Update | Happy path | PASS |
| REQ-CAT-03 Delete | Soft-delete only | PASS |
| REQ-CAT-04 Reorder | Happy path + incomplete list | PASS |

### REQ-BL-* BudgetLines

| Requirement | Scenarios covered | Status |
|-------------|-------------------|--------|
| REQ-BL-01 IsClosed guard | Create/Update/Delete blocked on closed period | PASS |
| REQ-BL-02 Create | With category + without + invalid LineType + invalid currency | PASS |
| REQ-BL-03 Update | Amount change creates new revision; latest shown in read | PASS |
| REQ-BL-04 Delete | Soft-delete line; revisions immutable per ADR-BS-01 | WARNING deviation-2 |

### REQ-READ-* Read Endpoints

| Requirement | Scenarios covered | Status |
|-------------|-------------------|--------|
| REQ-READ-01 ListCycles | Active flag + period count | PASS |
| REQ-READ-02 GetCycleDetail | Nested periods ordered by PeriodNumber + 404 | PASS |
| REQ-READ-03 ListCategoryGroups | Nested categories ordered by DisplayOrder | PASS |
| REQ-READ-04 ListBudgetLines | Latest revision + viewer read + admin write | PASS |

---

## Deviation Verdicts

### Deviation 1: SetActiveCycle Explicit Transaction (ADR-BS-03)

ADR-BS-03 stated: single SaveChangesAsync = single transaction.
Implementation: explicit BeginTransactionAsync + two sequential SaveChangesAsync (deactivate, then activate).

The partial unique index IX_Cycles_BudgetId_IsActive WHERE IsActive=true prevents two active cycles.
EF Core batching both UPDATEs in one SaveChangesAsync may execute activate before deactivate,
violating the constraint. The explicit two-step transaction is the correct fix.
Atomicity is fully preserved - both saves run inside the same database transaction.

VERDICT: ACCEPT. Implementation is correct and superior to the ADR.
Action: Update ADR-BS-03 to reflect explicit-transaction pattern and constraint-ordering rationale.
Evidence: SetActiveCycleHandler.cs lines 27-43; SetActiveCycle_AtomicSwap_Returns200 confirms atomicity.

### Deviation 2: BudgetLineRevision Soft Delete (REQ-BL-04 vs ADR-BS-01)

Spec REQ-BL-04: cascade-soft-delete its Revisions.
ADR-BS-01: BudgetLineRevision has no soft delete (immutable append-only).
Implementation follows ADR-BS-01: DeleteBudgetLineHandler sets DeletedAt on BudgetLine only.

When a BudgetLine is soft-deleted its Revisions become inaccessible because ListBudgetLines JOINs
on non-deleted BudgetLines. No independent read path exists for orphaned revisions.

VERDICT: ACCEPT. ADR-BS-01 wins over imprecise spec wording.
Action: Update spec REQ-BL-04 to reflect immutability; remove cascade-soft-delete Revisions claim.
Note: REQ-BL-04 restore scenario has no /restore endpoint. Spec says MAY. Deferred. See W-02.

### Deviation 3: Invalid LineType returns HTTP 400 not 422

Spec @unit scenario: THEN HTTP 422 with validation error on LineType.
Implementation: JsonStringEnumConverter rejects unknown enum names at deserialization -> HTTP 400.

FluentValidation unit tests correctly cover LineType rejection at the domain layer.
Integration test CreateBudgetLine_InvalidLineType_Returns400 documents the actual HTTP behavior.

VERDICT: ACCEPT. Behavior is correct for serializer-first architecture.
Action: Update spec scenario HTTP expected from 422 to 400 with note on JSON deserialization.

---

## Issues

### WARNINGS

| ID | Location | Finding |
|----|----------|---------|
| W-01 | spec.md REQ-BL-04 | cascade-soft-delete Revisions wording conflicts with ADR-BS-01. Spec must be corrected. |
| W-02 | spec.md REQ-BL-04 restore | No /restore endpoint. Deferred (spec says MAY). No test covers restore. |
| W-03 | spec.md REQ-BL-02 LineType | Spec says HTTP 422; actual HTTP 400. Spec must be updated. |
| W-04 | design.md ADR-BS-03 | ADR states single SaveChangesAsync; explicit two-step tx used. ADR must be updated. |
| W-05 | All projects NuGet | SQLitePCLRaw 2.1.11 high severity GHSA-2m69-gcr7-jv3q. Pre-existing; not in this change. |

### SUGGESTIONS

| ID | Location | Finding |
|----|----------|---------|
| S-01 | Delete* handlers | IgnoreQueryFilters + manual DeletedAt cascade repeated. Shared extension reduces duplication. Non-blocking. |
| S-02 | CreateBudgetLineHandler | Two sequential SaveChangesAsync. Domain factory with single save would be cleaner. Non-blocking. |
| S-03 | BudgetLineTests | No test for restore scenario. Add TODO in spec/tasks if deferred. |

---

## TDD Compliance

| Check | Result | Details |
|-------|--------|---------|
| TDD Evidence reported | PASS | Found in apply-progress artifact |
| All tasks have tests | PASS | 19 unit test folders + 8 integration test files verified on disk |
| RED confirmed tests exist | PASS | All test files verified present |
| GREEN confirmed tests pass | PASS | 170/170 unit pass live; 90/90 integration confirmed |
| Triangulation adequate | PASS | Happy path + error cases + multiple field variants |
| Safety Net for modified files | PASS | Pre-existing 170 unit tests green before PR4 additions |

TDD Compliance: 6/6 checks passed

---

## Test Layer Distribution

| Layer | Tests | Files | Tools |
|-------|-------|-------|-------|
| Unit | 170 | 19 folders | xUnit + Shouldly + EF SQLite in-memory |
| Integration | 90 | 8 files | xUnit + Testcontainers PostgreSQL |
| E2E | 0 | - | Not in scope |
| Total | 260 | 27 | |

---

## Assertion Quality

No tautologies. No ghost loops. No type-only assertions used alone.
All assertions verify real behavior: error codes, monetary amounts, display orders,
nested array lengths, IsActive/IsClosed states against concrete expected values.

Assertion quality: All assertions verify real behavior

---

## Design Coherence

| ADR | Status |
|-----|--------|
| ADR-BS-01 Soft delete + query filter | 5 entities have DeletedAt; BudgetLineRevision has none. PASS |
| ADR-BS-02 Cascade strategy | Migration FK rules match design table Cascade/Restrict. PASS |
| ADR-BS-03 SetActiveCycle atomicity | Explicit transaction accepted per deviation 1. ADR update needed. PASS |
| ADR-BS-04 Reorder via ordered ID list | Handler validates completeness + duplicates; assigns i+1. PASS |
| ADR-BS-05 IsClosed guard per-handler | Guard present in Create/Update/Delete BudgetLine handlers. PASS |
| ADR-BS-06 Revision auto-create | Create and Update both insert new BudgetLineRevision row. PASS |
| ADR-BS-07 Dapper for reads | All 4 read handlers use ConnectionFactory + raw SQL with LATERAL JOIN. PASS |
| ADR-BS-08 Resource isolation | All write handlers verify ownership chain entity.BudgetId == routeBudgetId. PASS |
| ADR-BS-09 i18n deferred | Hardcoded error code strings; no .resx files. PASS |

---

## Follow-up Actions Required Before Archive

1. Update openspec/changes/budget-structure/spec.md:
   - REQ-BL-04: replace cascade-soft-delete Revisions with immutability note
   - REQ-BL-04 restore scenario: mark as DEFERRED
   - REQ-BL-02 invalid LineType: update expected HTTP from 422 to 400

2. Update openspec/changes/budget-structure/design.md ADR-BS-03:
   Replace single SaveChangesAsync with explicit transaction + two sequential SaveChangesAsync
   (deactivate first then activate) required by partial unique index IX_Cycles_BudgetId_IsActive.

Documentation corrections only. No code changes required.

---

## Overall Verdict

**PASS WITH WARNINGS**

- 0 CRITICAL issues
- 5 WARNINGS (documentation and spec corrections only; no code changes required)
- 3 SUGGESTIONS (non-blocking refactors)

All 58 tasks complete and verified. All 23 endpoints implemented with correct RBAC, IsClosed guards,
cascade behavior, and ownership chain validation. 260 tests pass with 0 failures. Three known
deviations are all accepted: two require spec/ADR corrections, one (restore endpoint) is a deferred
optional capability.

The change is ready for sdd-archive after spec/ADR documentation corrections are applied.
