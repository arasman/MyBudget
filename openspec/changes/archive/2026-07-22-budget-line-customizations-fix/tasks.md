# Tasks: Budget Line Customizations

## PR Chain
PR1 → PR2a → PR2b → PR3 → feat/budget-line-customizations → main

## PR Workload

| Unit | Goal | PR | Base branch |
|---|---|---|---|
| 1 | Frontend customizations view | PR1 | feat/budget-line-customizations |
| 2a | Domain methods + EF xmin concurrency | PR2a | PR1 branch |
| 2b | 4 VSA slices + audit log | PR2b | PR2a branch |
| 3 | Restore validation | PR3 | PR2b branch |

---

## PR1 — Frontend Customizations View (base: feat/budget-line-customizations)

Spec: REQ-BLR-05, REQ-BLR-04 (i18n only)

### Phase 1 — Types and API Layer
- [x] T1.1 [RED] BudgetLineRevisionResponse type test in budget-structure/types.spec.ts
- [x] T1.2 [GREEN] Add BudgetLineRevisionResponse to types.ts
- [x] T1.3 [RED] Vitest tests for listRevisions, createRevision, deleteRevision in budgetLines.api.spec.ts
- [x] T1.4 [GREEN] Add revision functions to budgetLines.api.ts

### Phase 2 — Store
- [x] T1.5 [RED] Vitest tests for fetchRevisions, createRevision, deleteRevision actions in store.spec.ts
- [x] T1.6 [GREEN] Add revisions ref + actions to store.ts

### Phase 3 — Router and View
- [x] T1.7 [RED] Route test: lines/:lineId/customizations resolves to BudgetLineCustomizationsView
- [x] T1.8 [GREEN] Register child route in router/index.ts (BEFORE modal strip)
- [x] T1.9 [RED] Component test for BudgetLineCustomizationsView.vue
- [x] T1.10 [GREEN] Create BudgetLineCustomizationsView.vue
- [x] T1.11 [RED] BudgetLinesView nav link test
- [x] T1.12 [GREEN] Add nav link per row to BudgetLinesView.vue

### Phase 4 — Modal Strip
- [x] T1.13 [RED] BudgetLineModal edit mode: assert no validFrom/validTo/newAmount fields
- [x] T1.14 [GREEN] Remove Amount Revision section from BudgetLineModal.vue

### Phase 5 — i18n Keys
- [x] T1.15 [RED] All 5 error keys present in en.json + es.json
- [x] T1.16 [GREEN] Add i18n keys to both locale files

### Phase 6 — Verification
- [x] T1.17 Run npx vitest run — all PR1 tests green (47 files, 382 tests)

---

## PR2a — Domain Methods + EF Concurrency (base: PR1 branch)

Spec: REQ-BL-DATERANGE-1, REQ-BL-CONCURRENCY-1, REQ-BLR-02, REQ-BLR-03

- [x] T2.1 [RED] DeleteRevision unit tests: middle repair, last revision, original blocked, active executions blocked, soft-deleted pass
- [x] T2.2 [GREEN] Implement BudgetLine.DeleteRevision(Guid revisionId, bool hasActiveExecutions)
- [x] T2.3 [RED] UpdateDateRange unit tests: valid shrink, orphan revision guards
- [x] T2.4 [GREEN] Implement BudgetLine.UpdateDateRange(DateOnly startDate, DateOnly? endDate)
- [x] T2.5 [RED] SQLite persistence test: loading BudgetLine on SQLite does NOT throw on missing xmin column
- [x] T2.6 [GREEN] Provider-conditional xmin config in AppDbContext.OnModelCreating (Npgsql 10: manual shadow property xmin with xid type; SQLite skips)
- [x] T2a.V Run dotnet test — 409 tests, 0 failures, all PR2a tests green

---

## PR2b — VSA Slices + Audit Log (base: PR2a branch)

Spec: REQ-BLR-01, REQ-BLR-02, REQ-BLR-03, REQ-BL-AUDIT-1

- [x] T2.7 [RED] Integration tests: GET .../revisions (200, 404, 401, 403)
- [x] T2.8 [GREEN] Create ListBudgetLineRevisions slice (4 files)
- [x] T2.9 [RED] Validator unit tests: CreateBudgetLineRevisionCommand (validFrom, amount)
- [x] T2.10 [GREEN] CreateBudgetLineRevisionValidator.cs
- [x] T2.11 [RED] Integration tests: POST .../revisions (201, 409 xmin)
- [x] T2.12 [GREEN] CreateBudgetLineRevision slice (3 files)
- [x] T2.13 [RED] Integration tests: DELETE .../revisions (204, 422, 409, soft-delete, audit, xmin)
- [x] T2.14 [GREEN] DeleteBudgetLineRevision slice with explicit Remove + explicit AuditLog.Create
- [x] T2.15 [RED] Integration tests: PATCH .../date-range (200, 422, 409, soft-delete, xmin)
- [x] T2.16 [GREEN] UpdateBudgetLineDateRange slice (4 files)
- [ ] T2b.V Run dotnet test — all PR2b integration tests green; concurrency tests on SQLite marked skip [PENDING — requires Docker PostgreSQL stack]

---

## PR3 — Restore Validation (base: PR2b branch)

Spec: REQ-EXEC-RESTORE-DATERANGE-1

- [x] T3.1 [RED] Unit tests: RestoreExecutionRecordHandler date-range guard (4 scenarios)
- [x] T3.2 [RED] Integration tests: POST .../executions/{id}/restore (4 scenarios)
- [x] T3.3 [GREEN] Modify RestoreExecutionRecordHandler.cs — load BudgetLine, add date-range intersection check after IsClosed check
- [x] T3.4 Run dotnet test — all PR3 tests green, no regressions

---

## Known Gotchas

- SQLite xmin: skip concurrency 409 tests — mark [Fact(Skip = "xmin concurrency requires PostgreSQL")]
- EF tracking: after BudgetLine.DeleteRevision(), handler MUST call _db.BudgetLineRevisions.Remove(target) explicitly
- Modal strip ordering: register route T1.8 BEFORE stripping modal T1.14 (DONE in PR1)
- Audit for revision delete: write AuditLog.Create() explicitly in handler — DO NOT rely on SaveChangesAsync interceptor
- HTTP codes: RANGE_WOULD_ORPHAN_REVISION, CANNOT_DELETE_ORIGINAL_REVISION, EXECUTION_OUT_OF_DATE_RANGE → 422; RANGE_WOULD_ORPHAN_EXECUTION, REVISION_HAS_ACTIVE_EXECUTIONS, xmin conflict → 409
- UseXminAsConcurrencyToken() REMOVED in Npgsql 10 — use manual shadow property: builder.Property<uint>("xmin").HasColumnType("xid").ValueGeneratedOnAddOrUpdate().IsConcurrencyToken()
- DeleteRevision signature: takes bool hasActiveExecutions (not Func/IQueryable) — handler resolves the DB check before calling domain method
- EntryType enum: Expense, CreditNote, DebitNote (NOT Actual) — important for integration test seeds
- AuditLog namespace conflict: use `Entities = MyBudget.Features.SharedKernel.Entities` alias when referencing AuditLog.Create in handlers under Features namespace
