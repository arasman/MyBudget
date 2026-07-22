# Archive Report: budget-line-customizations

**Change**: `budget-line-customizations`
**Archived**: 2026-07-22
**Branch**: feat/budget-line-customizations
**Verdict**: PASS

---

## Commit Traceability

| PR | Commit | Scope |
|---|---|---|
| PR1 | d192a4a | Frontend customizations view (Vitest: 382 tests) |
| PR2a | 3a2915c | Domain methods + EF xmin concurrency (unit: 409 tests) |
| PR2b | 30952f0 | VSA slices, migrations, integration tests (193 integration) |
| PR3 | 201f475 | RestoreExecutionRecord date-range guard (4 unit + 3 integration) |

---

## Final Test Evidence

| Suite | Count | Result |
|---|---|---|
| Frontend (Vitest) | 382 tests, 47 files | PASS |
| Backend unit (SQLite) | 419 tests | PASS |
| Backend integration | 193 (190 pass, 3 skip) | PASS |
| Build | Clean | PASS |

Skips: 3 xmin concurrency tests (PostgreSQL-only, expected on SQLite).

---

## Capabilities Delivered

- **budget-line-revisions** (new domain): List/Create/Delete revision endpoints, UpdateDateRange endpoint, xmin optimistic concurrency, audit log for mutations
- **budget-structure** (delta): `BudgetLine.UpdateDateRange()`, `BudgetLine.DeleteRevision()` domain methods; xmin shadow property concurrency token; AuditLog.Action widened to varchar(50)
- **budget-execution** (delta): `RestoreExecutionRecord` now guards against restoring records whose Period falls outside BudgetLine date range

---

## New Specs Merged

- `openspec/specs/budget-line-revisions/spec.md` — created (REQ-BLR-01 through REQ-BLR-05)
- `openspec/specs/budget-structure/spec.md` — delta: REQ-BL-DATERANGE-1, REQ-BL-CONCURRENCY-1, REQ-BL-AUDIT-1
- `openspec/specs/budget-execution/spec.md` — delta: REQ-EXEC-RESTORE-DATERANGE-1
