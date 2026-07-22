# Tasks: Budget Line Customizations

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 900–1 200 |
| 400-line budget risk | High |
| Chained PRs recommended | Yes |
| Suggested split | PR1 → PR2a → PR2b → PR3 (feature-branch-chain) |
| Delivery strategy | ask-on-risk → exception approved (PR2 split into 2a + 2b) |
| Chain strategy | feature-branch-chain |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: feature-branch-chain
400-line budget risk: High

### Suggested Work Units

| Unit | Goal | PR | Base branch | Notes |
|------|------|----|-------------|-------|
| 1 | Frontend customizations view | PR1 | `feat/budget-line-customizations` | Can be developed with mocked API |
| 2a | Domain methods + EF xmin concurrency | PR2a | PR1 branch | Pure domain logic — fast review |
| 2b | 4 VSA slices + audit log | PR2b | PR2a branch | Slice boilerplate — reviewable in isolation |
| 3 | Restore validation | PR3 | PR2b branch | Isolated handler delta; small diff |

All PRs merge sequentially into `feat/budget-line-customizations`; only the tracker branch merges to `main`.

---

## PR1 — Frontend Customizations View

> Base branch: `feat/budget-line-customizations`
> Spec: REQ-BLR-05, REQ-BLR-04 (i18n only)

### Phase 1 — Types and API Layer

- [x] T1.1 **[RED]** Write Vitest unit test asserting `BudgetLineRevisionResponse` type shape is importable and fields match design contract (`id`, `budgetedAmount`, `currencyId`, `validFrom`, `validTo`, `note`). Test in `budget-structure/types.spec.ts`.
- [x] T1.2 **[GREEN]** Add `BudgetLineRevisionResponse` interface to `frontend/src/features/budget-structure/types.ts`.
- [x] T1.3 **[RED]** Write Vitest tests for `listRevisions`, `createRevision`, `deleteRevision` in `budgetLines.api.spec.ts` using `vi.fn()` mocks; assert correct HTTP methods, paths, and payload shapes.
- [x] T1.4 **[GREEN]** Add `listRevisions(budgetId, lineId)`, `createRevision(budgetId, lineId, payload)`, `deleteRevision(budgetId, lineId, revisionId)` functions to `frontend/src/features/budget-structure/api/budgetLines.api.ts`.

### Phase 2 — Store

- [x] T1.5 **[RED]** Write Vitest tests for `fetchRevisions`, `createRevision`, `deleteRevision` actions in `store.spec.ts`; mock API calls; assert `revisions` ref state after each action.
- [x] T1.6 **[GREEN]** Add `revisions` ref and `fetchRevisions`, `createRevision`, `deleteRevision` actions to `frontend/src/features/budget-structure/store.ts`.

### Phase 3 — Router and View

- [x] T1.7 **[RED]** Write Vitest test asserting `lines/:lineId/customizations` child route exists in router config and resolves to `BudgetLineCustomizationsView`.
- [x] T1.8 **[GREEN]** Register child route `lines/:lineId/customizations` in `frontend/src/router/index.ts` pointing to `BudgetLineCustomizationsView` — wire this BEFORE stripping the modal section (see T1.11).
- [x] T1.9 **[RED]** Write Vitest component test for `BudgetLineCustomizationsView.vue`: assert revision table renders columns `ValidFrom`, `ValidTo`, `Amount`, `Currency`, `Note`; assert inline create form; assert delete button per row calls `deleteRevision`.
- [x] T1.10 **[GREEN]** Create `frontend/src/features/budget-structure/views/BudgetLineCustomizationsView.vue` with revision table and inline create/delete form wired to store actions.
- [x] T1.11 **[RED]** Write Vitest test for `BudgetLinesView.vue`: assert nav link per row pointing to `lines/:lineId/customizations` is rendered.
- [x] T1.12 **[GREEN]** Add customizations nav link per row to `frontend/src/features/budget-structure/views/BudgetLinesView.vue`.

### Phase 4 — Modal Strip

- [x] T1.13 **[RED]** Write Vitest test for `BudgetLineModal.vue` in edit mode: assert no `validFrom`, `validTo`, or `newAmount` fields are present in the DOM (REQ-BLR-05 scenario).
- [x] T1.14 **[GREEN]** Remove Amount Revision section (`validFrom`, `validTo`, `newAmount`) from edit mode in `frontend/src/features/budget-structure/components/BudgetLineModal.vue`.

### Phase 5 — i18n Keys

- [x] T1.15 **[RED]** Write Vitest test asserting all 5 error keys (`cannotDeleteOriginalRevision`, `revisionHasActiveExecutions`, `rangeWouldOrphanRevision`, `rangeWouldOrphanExecution`, `executionOutOfDateRange`) under `budgetLineRevisions.errors` are present and non-empty in `en.json` and `es.json`.
- [x] T1.16 **[GREEN]** Add i18n keys for all 5 error codes to both `en.json` and `es.json` locale files (REQ-BLR-04).

### Phase 6 — PR1 Verification

- [x] T1.17 Run `npx vitest run` — all PR1 tests green (47 files, 382 tests).

---

## PR2a — Domain Methods + EF Concurrency

> Base branch: PR1 branch
> Spec: REQ-BL-DATERANGE-1, REQ-BL-CONCURRENCY-1, REQ-BLR-02, REQ-BLR-03

### Phase 1 — Domain Methods

- [x] T2.1 **[RED]** Write xUnit unit tests in `BudgetLineDeleteRevisionTests.cs` for `DeleteRevision`: (a) middle revision gapless repair — predecessor `ValidTo` extends; (b) last revision deleted — predecessor becomes open-ended (`ValidTo = null`); (c) original revision rejected — `CANNOT_DELETE_ORIGINAL_REVISION`; (d) active execution in range — `REVISION_HAS_ACTIVE_EXECUTIONS`; (e) soft-deleted execution in range — delete succeeds.
- [x] T2.2 **[GREEN]** Implement `BudgetLine.DeleteRevision(Guid revisionId, bool hasActiveExecutions)` in `SharedKernel/Entities/BudgetLine.cs`; returns removed `BudgetLineRevision` entity; applies gapless repair logic.
- [x] T2.3 **[RED]** Write xUnit unit tests for `UpdateDateRange` in `BudgetLineUpdateDateRangeTests.cs`: (a) valid shrink succeeds; (b) revision `ValidFrom` before new start — `RANGE_WOULD_ORPHAN_REVISION`; (c) revision `ValidTo` after new end — `RANGE_WOULD_ORPHAN_REVISION`.
- [x] T2.4 **[GREEN]** Implement `BudgetLine.UpdateDateRange(DateOnly startDate, DateOnly? endDate)` in `SharedKernel/Entities/BudgetLine.cs`.

### Phase 2 — EF Concurrency Config

- [x] T2.5 **[RED]** Write SQLite persistence test in `BudgetLineXminConcurrencyTests.cs` asserting that loading a `BudgetLine` on SQLite does NOT throw on missing `xmin` column.
- [x] T2.6 **[GREEN]** Add provider-conditional xmin concurrency config in `AppDbContext.OnModelCreating` — only applied when `Database.ProviderName` contains "Npgsql"; SQLite path skips it entirely. Note: `UseXminAsConcurrencyToken()` was removed in Npgsql 10; using manual shadow property `xmin` with `HasColumnType("xid").IsConcurrencyToken()` instead.

### Phase 3 — PR2a Verification

- [x] T2a.V Run `dotnet test tests/MyBudget.Features.Tests` — 409 tests, 0 failures. All 18 new PR2a tests green.

---

## PR2b — VSA Slices + Audit Log

> Base branch: PR2a branch
> Spec: REQ-BLR-01, REQ-BLR-02, REQ-BLR-03, REQ-BL-AUDIT-1

### Phase 1 — VSA Slices

#### ListBudgetLineRevisions

- [x] T2.7 **[RED]** Write integration tests for `GET /api/budgets/{budgetId}/lines/{lineId}/revisions`: (a) 200 + ordered revisions; (b) 404 when lineId not found; (c) 401 unauthenticated; (d) 403 insufficient role.
- [x] T2.8 **[GREEN]** Create `Features/BudgetStructure/ListBudgetLineRevisions/` — `ListBudgetLineRevisionsQuery.cs`, `ListBudgetLineRevisionsHandler.cs` (Dapper), `ListBudgetLineRevisionsEndpoint.cs`, and `RevisionDto.cs`.

#### CreateBudgetLineRevision

- [x] T2.9 **[RED]** Write xUnit validator tests for `CreateBudgetLineRevisionCommand`: (a) `validFrom` before today → 422; (b) `validFrom` outside BudgetLine range → 422; (c) `newAmount` ≤ 0 → 422.
- [x] T2.10 **[GREEN]** Create `Features/BudgetStructure/CreateBudgetLineRevision/CreateBudgetLineRevisionValidator.cs`.
- [x] T2.11 **[RED]** Write integration tests for `POST .../revisions`: (a) 201 + gapless chain; (b) 409 on stale `xmin` (PostgreSQL only — mark SQLite skip).
- [x] T2.12 **[GREEN]** Create `CreateBudgetLineRevisionCommand.cs`, `CreateBudgetLineRevisionHandler.cs` (calls `BudgetLine.SplitRevision`), `CreateBudgetLineRevisionEndpoint.cs`; catch `DbUpdateConcurrencyException` → 409.

#### DeleteBudgetLineRevision

- [x] T2.13 **[RED]** Write integration tests for `DELETE .../revisions/{revisionId}`: (a) 204 + gapless repair; (b) 422 `CANNOT_DELETE_ORIGINAL_REVISION`; (c) 409 `REVISION_HAS_ACTIVE_EXECUTIONS`; (d) soft-deleted executions → 204; (e) audit entry `BudgetLineRevisionDeleted` written; (f) 409 on stale `xmin` (mark SQLite skip).
- [x] T2.14 **[GREEN]** Create `DeleteBudgetLineRevisionCommand.cs`, `DeleteBudgetLineRevisionHandler.cs` (explicit `_db.BudgetLineRevisions.Remove(target)` + explicit `AuditLog.Create("BudgetLineRevisionDeleted", ...)` before `SaveChangesAsync`), `DeleteBudgetLineRevisionEndpoint.cs`.

#### UpdateBudgetLineDateRange

- [x] T2.15 **[RED]** Write integration tests for `PATCH .../date-range`: (a) 200 + EndDate updated + audit entry; (b) 422 `RANGE_WOULD_ORPHAN_REVISION`; (c) 409 `RANGE_WOULD_ORPHAN_EXECUTION`; (d) soft-deleted executions outside range → 200; (e) 409 on stale `xmin` (mark SQLite skip).
- [x] T2.16 **[GREEN]** Create `UpdateBudgetLineDateRangeCommand.cs`, `UpdateBudgetLineDateRangeValidator.cs`, `UpdateBudgetLineDateRangeHandler.cs` (loads line + revisions, calls `UpdateDateRange`, queries active executions for ORPHAN_EXECUTION guard, `SaveChangesAsync` triggers interceptor audit for `BudgetLineDateRangeUpdated`), `UpdateBudgetLineDateRangeEndpoint.cs`.

### Phase 2 — PR2b Verification

- [ ] T2b.V Run `dotnet test` — all PR2b integration tests green; concurrency tests on SQLite marked skip where noted. [PENDING — requires Docker PostgreSQL stack]

---

## PR3 — Restore Validation

> Base branch: PR2b branch
> Spec: REQ-EXEC-RESTORE-DATERANGE-1

### Phase 1 — Unit Tests

- [ ] T3.1 **[RED]** Write xUnit unit tests for `RestoreExecutionRecordHandler` date-range guard: (a) period within BudgetLine range → passes; (b) period starts before BudgetLine start → 422 `EXECUTION_OUT_OF_DATE_RANGE`; (c) period ends after BudgetLine end → 422; (d) `OperationDate` outside range but period inside → passes (REQ-EXEC-RESTORE-DATERANGE-1 OperationDate assertion).

### Phase 2 — Integration Tests

- [ ] T3.2 **[RED]** Write integration tests for `POST .../executions/{id}/restore`: (a) happy path — period within BudgetLine range → 200; (b) period before BudgetLine start → 422 `EXECUTION_OUT_OF_DATE_RANGE`; (c) period after BudgetLine end → 422; (d) `Period.IsClosed = true` still returns 409 `PERIOD_CLOSED` (existing guard not broken).

### Phase 3 — Implementation

- [ ] T3.3 **[GREEN]** Modify `Features/BudgetExecution/RestoreExecutionRecord/RestoreExecutionRecordHandler.cs`: load parent `BudgetLine`; after `IsClosed` check, assert `Period.StartDate >= BudgetLine.StartDate` and `Period.EndDate <= BudgetLine.EndDate` (when not null); return 422 `EXECUTION_OUT_OF_DATE_RANGE` on violation.

### Phase 4 — PR3 Verification

- [ ] T3.4 Run `dotnet test` — all PR3 tests green; no regressions on existing restore tests.

---

## Known Skips / Gotchas

- **SQLite xmin**: `UseXminAsConcurrencyToken()` is PostgreSQL-only. Integration tests on SQLite MUST skip or stub the 409-concurrency scenario. Mark each with `[Fact(Skip = "xmin concurrency requires PostgreSQL")]` or use provider-conditional test helpers.
- **EF tracking**: `Revisions.Remove(target)` inside `BudgetLine.DeleteRevision` removes from the nav collection but does NOT mark the entity `Deleted` in EF change tracking. `DeleteBudgetLineRevisionHandler` MUST call `_db.BudgetLineRevisions.Remove(target)` explicitly after `line.DeleteRevision(revisionId)`.
- **Modal strip ordering**: register the `lines/:lineId/customizations` route (T1.8) BEFORE removing the Amount Revision section from `BudgetLineModal` (T1.14). Reversing order risks a broken UI in intermediate commits.
- **Audit for revision delete**: do NOT rely on the `SaveChangesAsync` interceptor for `BudgetLineRevisionDeleted` — write `AuditLog.Create(...)` explicitly in the handler before `Remove()` + `SaveChangesAsync`.
- **HTTP codes**: `RANGE_WOULD_ORPHAN_REVISION` and `CANNOT_DELETE_ORIGINAL_REVISION` → 422; `RANGE_WOULD_ORPHAN_EXECUTION`, `REVISION_HAS_ACTIVE_EXECUTIONS`, and xmin concurrency conflict → 409; `EXECUTION_OUT_OF_DATE_RANGE` → 422.
