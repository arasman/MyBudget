# Archive Report: current-situation (Periodic Financial Snapshot)

## Change Summary

**Change**: current-situation
**Status**: ARCHIVED
**Completed**: 2026-07-29
**Date Prefix**: 2026-07-29

---

## Artifact Observation IDs

All SDD artifacts archived with full traceability:

| Artifact | Observation ID | Type | Status |
|----------|---|---|---|
| Proposal | 368 | architecture | archived |
| Spec | 369 | architecture | archived |
| Design | 370 | architecture | archived |
| Tasks | 371 | architecture | archived |
| Verify Report (PR2 Frontend) | 373 | architecture | archived |
| Archive Report | 376 | architecture | archived |

**Artifact Store Mode**: hybrid (Engram + OpenSpec filesystem)

---

## SDD Cycle Completion

### Phases Complete (7/7)

| Phase | Scope | Status |
|---|---|---|
| 1 | Backend Foundation (entities, EF configs, migration) | DONE |
| 2 | BankAccount API Slices (4 slices, 4-file VSA each) | DONE |
| 3 | CutRecord API Slices (4 slices, 3-4 files VSA each) | DONE |
| 4 | Backend Tests (unit + integration) | DONE |
| 5 | Frontend BankAccount Feature (CRUD + store + tests) | DONE |
| 6 | Frontend CurrentSituation Feature (views + components + i18n + tests) | DONE |
| 7 | E2E Tests (5 scenarios, 5 browser tests) | DONE |

### Delivery Strategy

**Chained PRs**: feature-branch-chain

| PR | Branch | Phases | Status | Commits |
|---|---|---|---|---|
| PR1 | feat/cs-backend | 1–4 (backend) | Merged | 9d98e24 |
| PR2 | feat/cs-frontend | 5–7 (frontend + E2E) | Merged | 67959e9 |

---

## Verification Summary

**Verify Timestamp**: 2026-07-29 (re-verify after post-commit UX changes)
**Verdict**: PASS WITH WARNINGS
**Critical Issues**: 0
**Warnings**: 2 (carry-forward)
**Suggestions**: 1 (carry-forward)

### Task Completion

All Phase 5–7 tasks marked complete (`[x]`). No unchecked implementation tasks.

| Phase | Tasks | Completed | Notes |
|---|---|---|---|
| Phase 5 | 5.1–5.9 (BankAccount frontend) | 9/9 | All checked |
| Phase 6 | 6.1–6.15 (CurrentSituation frontend) | 15/15 | All checked |
| Phase 7 | 7.1–7.5 (E2E tests) | 5/5 | All checked |

### Test Evidence

**Unit Tests**: 428/428 passing (vitest run, exit code 0)
**Test Files**: 56 test files, 0 failures
**Duration**: 90.06s

### Spec Compliance

All 7 requirements verified through code inspection:

- CS-6: Cut totals computation (TotalDeudaEnCurso = Remaining + TotalNegative) ✅
- CS-5: BalanceInPrimary via exchangeRate ✅
- CS-7: Save button wired to form via defineExpose ✅
- CS-7: i18n ES+EN all new keys present ✅
- CS-4: Delete modal confirmation daisyUI v5 layout ✅
- BA-* (BankAccount CRUD): All 4 slices implemented ✅

### Warnings Carried Forward

| ID | Category | Issue | Status |
|---|---|---|---|
| W-001 | WARNING | BankAccounts tab missing from BudgetTabs | Confirm in next cycle |
| W-002 | SUGGESTION | Alt-currency column guard could be stricter | Low impact, cosmetic |
| W-003 | WARNING | No browser-level E2E page navigation test | Document in next cycle |

---

## Specs Synced to Main

New main specs created in `openspec/specs/`:

| Domain | Action | Files | Details |
|---|---|---|---|
| bank-accounts | Created | `openspec/specs/bank-accounts/spec.md` | 4 requirements (BA-1 through BA-4) |
| current-situation | Created | `openspec/specs/current-situation/spec.md` | 8 requirements (CS-1 through CS-8) |

Both specs are full specifications (not deltas) copied from change folder. No merges were required as main specs did not previously exist.

### Spec Contents Summary

**bank-accounts**: CRUD operations for budget-scoped bank accounts with soft-delete.
**current-situation**: Cut record lifecycle, balance snapshots, execution summary, frontend views.

---

## Archive Contents Verification

**Archive Location**: `openspec/changes/archive/2026-07-29-current-situation/`

### Archived Artifacts

- explore.md — exploration summary
- proposal.md — intent, scope, approach, delivery strategy
- spec.md — 8 behavioral requirements (BA-1–BA-4, CS-1–CS-8)
- design.md — technical decisions, entity model, file structure
- tasks.md — 83 tasks across 7 phases (all checked complete)
- apply-progress.md — PR1 backend + PR2 frontend completion evidence
- verify-report-pr1.md — backend verification (PASS)
- verify-report-pr2.md — frontend verification (PASS WITH WARNINGS)
- archive-report.md — this file

### Active Changes Directory

**Status**: `openspec/changes/current-situation/` folder content has been moved to archive. No longer present in active changes.

---

## Implementation Summary

### Backend (PR1: feat/cs-backend)

**3 New Entities**:
- BankAccount: budget-scoped catalog with alias, currencyId, isPositive, displayOrder, soft-delete
- CutRecord: cut date + exchange rate per budget per calendar day
- CutBankAccount: balance snapshot for account+cut pair, BalanceInPrimary computed at write time

**8 API Slices** (VSA 4-file pattern):
1. CreateBankAccount (POST /api/budgets/{id}/bank-accounts)
2. ListBankAccounts (GET, soft-deleted excluded)
3. UpdateBankAccount (PUT, no CurrencyId change)
4. DeleteBankAccount (soft-delete)
5. UpsertCutRecord (PUT with active-period validation)
6. GetCutRecord (draft + clone-from-previous logic via Dapper CTE)
7. ListCutDates (dates list, ascending)
8. DeleteCutRecord (hard delete + CASCADE)

**Tests**: 49 new tests (unit + integration), all passing.

### Frontend (PR2: feat/cs-frontend)

**2 Feature Folders**:
- `bank-accounts/`: CRUD with Pinia store + BankAccountForm + BankAccountListView
- `current-situation/`: date-navigable cut form + views + components (CutDateNavigator, CutRecordForm, ExecutionSummaryPanel, CutTotalsPanel, DeleteCutModal)

**30 New Unit Tests** (vitest), all passing.

**5 E2E Scenarios** (Playwright):
1. bank-account-crud
2. cut-record-create
3. cut-record-navigation
4. cut-record-delete
5. cut-draft-clone

**i18n**: `currentSituation.*` and `bankAccount.*` namespaces, ES + EN.

---

## Next Planned SDD Cycle

**Change Name**: bank-account-restore
**Scope**: RestoreBankAccount slice + show-deleted UI for BankAccounts
**Parity**: Match BudgetLines soft-delete UX pattern
**Estimated Tasks**: 2–3 phases (backend slice, frontend UI, E2E)

---

## Archive Checklist

- [x] All task checkboxes verified complete (no stale tasks)
- [x] Verify report shows PASS (no CRITICAL issues)
- [x] Main specs created/merged (bank-accounts, current-situation)
- [x] Change folder archived to 2026-07-29-current-situation/
- [x] Archive report written with all observation IDs
- [x] Archive report persisted to Engram with topic_key
- [x] Artifact store mode: hybrid ✅ (OpenSpec folder created + Engram observation saved)

---

## Cycle Status

**Closed**: The current-situation change has completed the full SDD cycle:
1. Exploration ✅
2. Proposal ✅
3. Spec ✅
4. Design ✅
5. Tasks ✅
6. Apply ✅ (PR1 + PR2)
7. Verify ✅ (PASS WITH WARNINGS)
8. Archive ✅

The feature is production-ready. Two non-critical warnings (W-001 BankAccounts tab visibility, W-003 page navigation E2E) are documented for the next cycle.

**Ready for the next change**: bank-account-restore
