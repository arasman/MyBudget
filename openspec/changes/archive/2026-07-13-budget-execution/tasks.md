# Tasks: Budget Execution

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 900–1100 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (feat/budget-execution) → PR 2 (feat/budget-execution-pr2) |
| Delivery strategy | ask-on-risk |
| Chain strategy | feature-branch-chain |

Decision needed before apply: No
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Entity + EF config + migration + Create/Update/Delete slices + unit tests | PR 1 | Base = feat/budget-execution; self-contained; write-path only |
| 2 | List/Totals/Restore slices + cascade activations + integration tests + E2E | PR 2 | Base = PR 1 branch (feat/budget-execution-pr2); depends on PR 1 |

---

## Phase 1: Foundation (PR 1 — entity, enum, EF, migration)

- [x] 1.1 Create branch `feat/budget-execution` from `main`
- [x] 1.2 Create `src/MyBudget.Features/SharedKernel/Entities/EntryType.cs` — enum Expense=1, CreditNote=2, DebitNote=3 (REQ-EXEC-2)
- [x] 1.3 Create `src/MyBudget.Features/SharedKernel/Entities/ExecutionRecord.cs` — entity extending BaseEntity + IAuditableEntity; fields: BudgetId, PeriodId, BudgetLineId, EntryType, Amount, Note, CurrencyId, ExchangeRate, ExchangeRateTo, AccountId?, PaymentMethodId?, DeletedAt; static Create(), Update(), SoftDelete(), Restore() factory methods (REQ-EXEC-1)
- [x] 1.4 Modify `src/MyBudget.Features/SharedKernel/Persistence/AppDbContext.cs` — add `DbSet<ExecutionRecord> ExecutionRecords`
- [x] 1.5 Create `src/MyBudget.Features/SharedKernel/Persistence/Configurations/ExecutionRecordConfiguration.cs` — table name, decimal precision (18,2 / 18,6), varchar(500) Note, int EntryType conversion, FK on BudgetLineId/CurrencyId/PeriodId (Restrict), no FK on AccountId/PaymentMethodId, HasQueryFilter(DeletedAt==null), composite indexes: (BudgetLineId,DeletedAt), (BudgetLineId,DeletedAt,EntryType), (PeriodId,DeletedAt), (BudgetId) (REQ-EXEC-1)
- [x] 1.6 Generate EF Core migration `AddExecutionRecords` — `dotnet ef migrations add AddExecutionRecords --project src/MyBudget.Features --startup-project src/MyBudget.Api`; verify migration file creates table + all indexes

---

## Phase 2: Write Slices — Create / Update / Delete (PR 1)

- [x] 2.1 Create `Features/BudgetExecution/CreateExecutionRecord/CreateExecutionRecordCommand.cs` — record with BudgetId, PeriodId, BudgetLineId, EntryType, Amount, Note, CurrencyId, ExchangeRate, ExchangeRateTo, AccountId?, PaymentMethodId?; IRequest<Result<Guid>> (REQ-EXEC-CREATE-1)
- [x] 2.2 Create `Features/BudgetExecution/CreateExecutionRecord/CreateExecutionRecordValidator.cs` — Amount>0 (AMOUNT_MUST_BE_POSITIVE), Note required for CreditNote/DebitNote (NOTE_REQUIRED_FOR_ENTRY_TYPE), EntryType in valid range (REQ-EXEC-3, REQ-EXEC-4)
- [x] 2.3 Create `Features/BudgetExecution/CreateExecutionRecord/CreateExecutionRecordHandler.cs` — load BudgetLine.Include(l=>l.Period.Cycle); verify BudgetId + PeriodId match (PERIOD_MISMATCH/404); IsClosed guard (PERIOD_CLOSED 409); ExchangeRate pair rule against DefaultCurrencyId; BudgetLine soft-delete guard (PARENT_IS_DELETED 409); ExecutionRecord.Create(); SaveChangesAsync (REQ-EXEC-CLOSED-1, REQ-EXEC-5, REQ-EXEC-6, REQ-EXEC-7)
- [x] 2.4 Create `Features/BudgetExecution/CreateExecutionRecord/CreateExecutionRecordEndpoint.cs` — POST `/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions`; RequireAuthorization("budget:operator"); 201 Created with Guid (REQ-EXEC-CREATE-1)
- [x] 2.5 Create `Features/BudgetExecution/UpdateExecutionRecord/UpdateExecutionRecordCommand.cs` + Validator — same Amount/Note/ExchangeRate rules as Create; add ExecutionId field (REQ-EXEC-UPDATE-1, REQ-EXEC-UPDATE-2)
- [x] 2.6 Create `Features/BudgetExecution/UpdateExecutionRecord/UpdateExecutionRecordHandler.cs` — load record with guard (not deleted or 404); IsClosed guard; ExchangeRate pair rule; record.Update(...); SaveChangesAsync (REQ-EXEC-UPDATE-1)
- [x] 2.7 Create `Features/BudgetExecution/UpdateExecutionRecord/UpdateExecutionRecordEndpoint.cs` — PUT `/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{executionId}`; budget:operator; 200 OK (REQ-EXEC-UPDATE-1)
- [x] 2.8 Create `Features/BudgetExecution/DeleteExecutionRecord/DeleteExecutionRecordCommand.cs` + Handler — load non-deleted record (or 404); IsClosed guard; record.SoftDelete(); SaveChangesAsync; 204 No Content (REQ-EXEC-DELETE-1, REQ-EXEC-DELETE-2, REQ-EXEC-CLOSED-1)
- [x] 2.9 Create `Features/BudgetExecution/DeleteExecutionRecord/DeleteExecutionRecordEndpoint.cs` — DELETE `/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions/{executionId}`; budget:operator; 204 (REQ-EXEC-DELETE-1)

---

## Phase 3: Unit Tests — Write Slices (PR 1)

- [x] 3.1 `tests/MyBudget.Features.Tests/BudgetExecution/CreateExecutionRecord/CreateExecutionRecordValidatorTests.cs` — Amount=0 rejected (AMOUNT_MUST_BE_POSITIVE), Amount<0 rejected, Amount>0 passes, Note absent for CreditNote rejected (NOTE_REQUIRED_FOR_ENTRY_TYPE), Note absent for DebitNote rejected, Note absent for Expense passes (REQ-EXEC-3, REQ-EXEC-4)
- [x] 3.2 `tests/.../CreateExecutionRecord/CreateExecutionRecordHandlerTests.cs` — PeriodId mismatch returns PERIOD_MISMATCH; IsClosed=true returns PERIOD_CLOSED; valid command creates and persists record; BudgetLine soft-deleted returns PARENT_IS_DELETED (REQ-EXEC-7, REQ-EXEC-CLOSED-1, REQ-EXEC-CREATE-2)
- [x] 3.3 `tests/.../UpdateExecutionRecord/UpdateExecutionRecordValidatorTests.cs` + `HandlerTests.cs` — same Amount/Note rules; deleted record returns 404; IsClosed guard (REQ-EXEC-UPDATE-2)
- [x] 3.4 `tests/.../DeleteExecutionRecord/DeleteExecutionRecordHandlerTests.cs` — already-deleted returns 404; IsClosed returns PERIOD_CLOSED; valid delete sets DeletedAt (REQ-EXEC-DELETE-2, REQ-EXEC-CLOSED-1)
- [x] 3.5 `tests/.../DeleteExecutionRecord/DeleteExecutionRecordEndpointTests.cs` — verify DELETE returns 204 on happy path; 404 for non-existent record; 403 without budget:operator (REQ-EXEC-DELETE-1)

---

## Phase 4: Read + Restore Slices (PR 2)

- [x] 4.1 Create branch `feat/budget-execution-pr2` from `feat/budget-execution`
- [x] 4.2 Create `Features/BudgetExecution/ListExecutionRecords/ListExecutionRecordsQuery.cs` + Handler (Dapper) — SELECT all non-deleted ExecutionRecords for BudgetLineId ORDER BY CreatedAt ASC; returns `List<ExecutionRecordDto>` (REQ-EXEC-LIST-1, REQ-EXEC-LIST-2)
- [x] 4.3 Create `Features/BudgetExecution/ListExecutionRecords/ListExecutionRecordsEndpoint.cs` — GET `/api/budgets/{budgetId}/periods/{periodId}/budget-lines/{lineId}/executions`; budget:read; 200 (REQ-EXEC-LIST-1)
- [x] 4.4 Create `Features/BudgetExecution/ListPeriodExecutionTotals/ListPeriodExecutionTotalsQuery.cs` + Handler (Dapper UNION ALL) — Part 1: GROUP BY BudgetLineId → LineTotalDto; Part 2: GROUP BY CategoryGroupId/CategoryId → CategoryTotalDto; currency conversion Amount/ExchangeRate when CurrencyId ≠ DefaultCurrencyId; GroupLevel discriminator splits rows (REQ-EXEC-TOTALS-1 to REQ-EXEC-TOTALS-4)
- [x] 4.5 Create `Features/BudgetExecution/ListPeriodExecutionTotals/ListPeriodExecutionTotalsEndpoint.cs` — GET `/api/budgets/{budgetId}/periods/{periodId}/execution-totals`; budget:read; 200 PeriodExecutionTotalsResponse (REQ-EXEC-TOTALS-1)
- [x] 4.6 Create `Features/BudgetExecution/RestoreExecutionRecord/RestoreExecutionRecordCommand.cs` + Validator (REQ-EXEC-RESTORE-1)
- [x] 4.7 Create `Features/BudgetExecution/RestoreExecutionRecord/RestoreExecutionRecordHandler.cs` — load soft-deleted record (non-deleted → 404); IsClosed guard; record.Restore(); SaveChangesAsync (REQ-EXEC-RESTORE-1, REQ-EXEC-RESTORE-2, REQ-EXEC-CLOSED-1)
- [x] 4.8 Create `Features/BudgetExecution/RestoreExecutionRecord/RestoreExecutionRecordEndpoint.cs` — POST `.../executions/{executionId}/restore`; budget:operator; 200 (REQ-EXEC-RESTORE-1)

---

## Phase 5: Cascade Activations (PR 2)

- [x] 5.1 Modify `Features/BudgetStructure/DeleteBudgetLine/DeleteBudgetLineHandler.cs` — after `line.SoftDelete()`, load `ExecutionRecords.Where(e=>e.BudgetLineId==lineId && e.DeletedAt==null)` with `IgnoreQueryFilters()`; call `e.SoftDelete()` on each; single `SaveChangesAsync` (REQ-EXEC-CASCADE-1)
- [x] 5.2 Modify `Features/BudgetStructure/RestoreBudgetLine/RestoreBudgetLineHandler.cs` — activate `IncludeExecutionRecords`: if `cmd.IncludeExecutionRecords`, load soft-deleted ExecutionRecords for lineId with `IgnoreQueryFilters()`; call `e.Restore()` on each (REQ-EXEC-CASCADE-2)
- [x] 5.3 Modify `Features/BudgetStructure/RestoreCategory/RestoreCategoryHandler.cs` — forward `IncludeExecutionRecords` flag through cascade to BudgetLine restore calls (REQ-EXEC-CASCADE-2)
- [x] 5.4 Modify `Features/BudgetStructure/RestoreCategoryGroup/RestoreCategoryGroupHandler.cs` — forward `IncludeExecutionRecords` flag (REQ-EXEC-CASCADE-2)
- [x] 5.5 Modify `Features/BudgetStructure/RestoreCycle/RestoreCycleHandler.cs` — forward `IncludeExecutionRecords` flag (REQ-EXEC-CASCADE-2)

---

## Phase 6: Integration Tests (PR 2)

- [x] 6.1 `tests/MyBudget.Integration.Tests/BudgetExecution/CreateExecutionRecordIntegrationTests.cs` — POST happy path returns 201 + Guid; RBAC: 403 without budget:operator (REQ-EXEC-CREATE-1)
- [x] 6.2 `tests/.../BudgetExecution/CreateExecutionRecordIntegrationTests.cs` — PERIOD_CLOSED returns 409; AMOUNT_MUST_BE_POSITIVE returns 400; NOTE_REQUIRED_FOR_ENTRY_TYPE returns 400 (REQ-EXEC-3, REQ-EXEC-4, REQ-EXEC-CLOSED-1)
- [x] 6.3 `tests/.../BudgetExecution/ListExecutionRecordsIntegrationTests.cs` — seed 3 records; GET returns all 3 ordered by CreatedAt ASC; 403 without budget:read (REQ-EXEC-LIST-1, REQ-EXEC-LIST-2)
- [x] 6.4 `tests/.../BudgetExecution/ListPeriodExecutionTotalsIntegrationTests.cs` — seed Expense + CreditNote for same line; verify lineTotals and categoryTotals shapes; netAmount formula correct (REQ-EXEC-TOTALS-1 to REQ-EXEC-TOTALS-4)
- [x] 6.5 `tests/.../BudgetExecution/RestoreExecutionRecordIntegrationTests.cs` — soft-delete then restore returns 200; restore non-deleted returns 404; IsClosed returns 409 (REQ-EXEC-RESTORE-1, REQ-EXEC-RESTORE-2)
- [x] 6.6 `tests/.../BudgetStructure/DeleteBudgetLine/DeleteBudgetLineHandlerCascadeTests.cs` — seed BudgetLine + 2 ExecutionRecords; soft-delete line; assert both ExecutionRecords.DeletedAt != null (REQ-EXEC-CASCADE-1)
- [x] 6.7 `tests/.../BudgetStructure/RestoreBudgetLine/RestoreBudgetLineWithExecutionsIntegrationTests.cs` — IncludeExecutionRecords=true restores child records; =false leaves them deleted (REQ-EXEC-CASCADE-2)

---

## Phase 7: E2E Tests — Playwright (PR 2)

- [x] 7.1 `e2e/budget-execution/create-execution.spec.ts` — authenticate; create ExecutionRecord (Expense) via POST; assert 201 + id returned
- [x] 7.2 `e2e/budget-execution/credit-debit-note.spec.ts` — create CreditNote with Note; create DebitNote with Note; verify both appear; attempt negative Amount → rejected
- [x] 7.3 `e2e/budget-execution/delete-restore.spec.ts` — delete execution; verify gone from list; restore; verify back
- [x] 7.4 `e2e/budget-execution/period-closed-guard.spec.ts` — close period; attempt create → 409; attempt update → 409
- [x] 7.5 `e2e/budget-execution/execution-totals.spec.ts` — add multiple entries; verify period totals reflect correct amounts (Expense+DebitNote-CreditNote)
- [x] 7.6 `e2e/budget-execution/rbac.spec.ts` — budget:read cannot create/update/delete; budget:operator can
