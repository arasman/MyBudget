# Verify Report: cut-record-totals-persistence

**Verdict**: PASS WITH WARNINGS
**Date**: 2026-07-30
**Branch**: feat/cut-record-totals-persistence (merged chain PR1+PR2+PR3, commits 170c35f..8482391)

## Executive Summary

19/19 tasks complete and verified against code. All 16 persisted total columns, the compute-at-write / read-verbatim architecture, the migration LATERAL-correlation fix, and all CS-1/CS-2/CS-6/CS-9 spec scenarios are implemented and covered by passing tests. Independent full-suite run: dotnet test 721 total/718 passed/3 skipped/0 failed (backend); 57 files/442 tests passed (frontend unit). 0 CRITICAL, 1 WARNING, 1 SUGGESTION.

## Completeness (tasks.md)

19/19 checked, all verified present in code:
- Phase 1 (1.1-1.4): CutTotals.cs, CutRecord.cs 16 props + Create/Update, CutRecordConfiguration.cs 16x HasPrecision(18,2).IsRequired(), migration 3-phase (nullable -> backfill -> non-nullable) -- all present.
- Phase 2 (2.1-2.2): CutTotalsCalculator.cs, BudgetExecutionSummaryQuery.cs -- present, pure/static as designed.
- Phase 3 (3.1-3.2): UpsertCutRecordHandler.cs reordered per Decision 4, GetCutRecordHandler.cs existing/draft split -- present.
- Phase 4 (4.1-4.9): CutTotalsCalculatorTests.cs (6 facts) + CutRecordIntegrationTests.cs (23 facts incl. all CS-1/CS-2/CS-6/CS-9 scenarios) -- present, all green.
- Phase 5 (5.1-5.2): useCutRecordStore.spec.ts regression (8/8), cut-totals-snapshot.spec.ts E2E -- present.
- Phase 6 (6.1): en.json/es.json snapshotNotice key + locales.spec.ts parity test -- present.

## Code Verification

### CS-6 -- 16 columns
Confirmed on CutRecord.cs: TotalPositive/Alt, TotalNegative/Alt, TotalDeudaEnCurso/Alt, TotalBudgeted/Alt, TotalRegistered/Alt, Remaining/Alt, TotalAvailable/Alt, TotalNet/Alt -- exact CS-6 names, all decimal, all private set. CutRecordConfiguration.cs: all 16 .HasPrecision(18, 2).IsRequired(). Migration Phase C: all 16 AlterColumn(..., nullable: false). Matches spec exactly.

### UpsertCutRecordHandler
Computes via BudgetExecutionSummaryQuery.ExecuteAsync + CutTotalsCalculator.Compute, persists via CutRecord.Create/Update(totals). UpsertCutRecordCommand/UpsertCutRecordRequest (endpoint DTO) have no total fields at all -- structurally impossible to read totals from the request body, stronger than simply ignoring them. Confirmed by test UpsertCutRecord_ClientSubmittedTotals_AreIgnored.

### GetCutRecordHandler
Existing-record path: single SQL SELECT of the 16 header columns + CutBankAccounts, zero calls to BudgetExecutionSummaryQuery or LINQ sums. Draft path: calls both shared components (live). Confirmed structurally and by test GetCutRecord_Existing_ReturnsStoredColumnsVerbatim_NotRecomputed, which overwrites the persisted row with an out-of-band marker (424242.42) and asserts GET returns the marker, not a recomputed value -- a strong regression guard against silently reintroducing live recomputation.

### Migration (20260730233923_AddCutRecordPersistedTotals.cs)
Three-phase structure confirmed (AddColumn nullable, then SQL backfill, then AlterColumn non-nullable), matching design.md Decision 6. LATERAL correlation bug fix confirmed present: the backfill LEFT JOIN LATERAL subquery correlates against src (a CutRecords src alias inside the derived table x own FROM list), not against the UPDATE target cr -- avoiding Postgres 42P10. Inline comment in the migration explicitly documents this constraint. The exact same SQL (BackfillSql) is executed by integration test MigrationBackfill_PreSeededRowsWithoutTotals_BackfilledToMatchPreChangeOutput, which passed in the independent full-suite run -- this is real runtime proof the fix works, not just static inspection. Down() drops all 16 columns.

### Spec scenario to test mapping (all verified passing)
- CS-1: UpsertCutRecord_ValidPayloadWithActivePeriod_Returns200, _Replace_OverwritesAllCutBankAccountRows, _NoActivePeriod_Returns422, _ReadRole_Returns403, _ClientSubmittedTotals_AreIgnored, _ReSave_OverwritesAllSixteenPersistedTotals, _PersistedTotals_EqualFreshlyComputedTotals.
- CS-2: GetCutRecord_Existing_ReturnsPersistedBalancesAndIsDraftFalse, _ReturnsStoredColumnsVerbatim_NotRecomputed, _Draft_FirstEver_AllActiveAccountsWithZeroBalance, _Draft_ClonedFromPreviousCut_WithNewAccountAtZero, _Draft_SoftDeletedAccountExcluded, _NoActivePeriod_ExecutionSummaryIsZero, _Draft_ComputesAllEightTotalConceptsLive.
- CS-6: CutTotalsCalculatorTests.Compute_Cs6TableCase_ReturnsExpectedTotals (exact 500/200/300 to 500/200/500 spec example), rounding tests (half-away-from-zero, positive and negative midpoints), zero/negative exchange-rate guard, empty-rows to Zero; integration PersistedCutTotals_UnaffectedByLaterAccountOrExecutionEdits (also mirrored at E2E level in cut-totals-snapshot.spec.ts, previously reported 1/1 passed).
- CS-9 (ADDED, confirmed present in spec.md): MigrationBackfill_PreSeededRowsWithoutTotals_BackfilledToMatchPreChangeOutput, MigrationBackfill_AllSixteenColumnsAreNonNullableInSchema.

### Shared components vs design.md Interfaces/Contracts
CutTotals record: 16-decimal positional record with Zero, matches design.md code block exactly (same parameter order/names). CutTotalsCalculator.Compute(rows, summary, exchangeRate) returning CutTotals: signature matches; er less-or-equal 0 -> 1m guard present; Math.Round(v, 2, MidpointRounding.AwayFromZero) applied to all 16. BudgetExecutionSummaryQuery.ExecuteAsync(IDbConnection, budgetId, cutDate): CTE moved verbatim from the pre-change GetCutRecordHandler per design.md Decision 3 (same active_period/budgeted/registered CTE structure now shared by Upsert and the GET draft path).

### Frontend DTO shape -- genuinely unchanged
git diff 170c35f..8482391 for Project/frontend/src/features/current-situation/types/cutRecord.ts returns zero diff. Backend-side DTO source (GetCutRecordQuery.cs, defining GetCutRecordResponse/CutTotalsDto/BudgetExecutionSummaryDto) also returns zero diff across the whole change (only GetCutRecordHandler.cs, the query logic, changed). This independently corroborates design.md explicit claim.

Frontend diff for the whole change is exactly 4 files: cut-totals-snapshot.spec.ts (new), locales.spec.ts (+16 lines, parity test), en.json/es.json (+1 key each) -- matches the proposal "frontend unchanged" scope precisely.

## Independent Test Run (this session)

- dotnet build MyBudget.slnx: initially failed -- a stray leftover MyBudget.Api.exe process (PID 30900, orphaned from a prior E2E session) held a file lock on MyBudget.Features.dll. Killed the process; rebuild succeeded clean (1 pre-existing unrelated warning: CS0108 in CreateExecutionRecordIntegrationTests.cs, not part of this change).
- dotnet test MyBudget.slnx: 721 total / 718 passed / 3 skipped / 0 failed (474 MyBudget.Features.Tests + 247 MyBudget.Integration.Tests, 3 skips are pre-existing concurrency-conflict tests unrelated to this change). This is an exact independent match to the number the user reported manually (721 total/718 passed/3 skipped/0 failed).
- npm run test (frontend, vitest): 57 files / 442 tests passed, 0 failed, including useCutRecordStore.spec.ts (8/8) and locales.spec.ts (48/48, includes the 2 new snapshot-notice parity assertions).
- E2E (cut-totals-snapshot.spec.ts): not re-run live in this verify pass (would require standing up the API + Vite dev server against the E2E DB); relying on the apply-progress artifact previously-reported result (1/1 new spec passed, 11/11 full e2e/current-situation regression passed, 0 failures) plus static review of the spec file, which correctly targets the GET response fields (totals, executionSummary) that CurrentSituationView.vue passes verbatim to CutTotalsPanel.

## Issues

### WARNING
i18n snapshot-notice copy added but not wired into any UI component. currentSituation.totals.snapshotNotice exists in both en.json and es.json (task 6.1, confirmed) and has a parity test, but a repo-wide search for snapshotNotice finds it only in the two locale files and the parity test -- no Vue component (CutTotalsPanel.vue, CurrentSituationView.vue, etc.) references the key via the i18n translate function. The proposal stated success criterion is "Snapshot semantics documented in spec and reflected in ES + EN UI copy" -- the spec documentation part is done (CS-1/CS-6 prose), but the copy is not yet visible to any user, so "reflected in UI copy" is not literally true yet. This is a known, explicitly-accepted gap: design.md Open Questions flags it as a copy-only change that can slip to its own slice. Not a regression risk and not spec-breaking (CS-1/CS-2/CS-6/CS-9 requirements do not mandate UI wiring), but it should be either (a) tracked as a fast-follow task before closing the proposal own success-criteria checklist, or (b) the proposal checklist item explicitly marked partial/deferred at archive time so it is not silently forgotten.

### SUGGESTION
A stray orphaned MyBudget.Api.exe process from a prior session blocked this session build until manually killed. Not a code defect, but worth a process hygiene note: background API/Vite processes started for E2E runs should be confirmed killed (not just attempted) before ending a session, since a locked DLL silently blocks the next dotnet build/dotnet test invocation with a moderately confusing MSB3027 error.

## Verdict

PASS WITH WARNINGS -- 0 CRITICAL, 1 WARNING, 1 SUGGESTION. Safe to proceed to sdd-archive. The WARNING is a documentation/UX-completeness gap already flagged as an accepted deferral in design.md, not a functional or spec-compliance defect.
