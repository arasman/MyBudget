# Tasks: budget-line-description

**Delivery**: single-pr  
**TDD**: OFF — tests must not break; no test-first requirement  
**Base migration**: `20260722041748_WidenAuditLogActionColumn`

---

## Execution Order

Tasks with the same group number can start in parallel once their prerequisite group is complete.

```
Group 1 (parallel): T1, T2
       |
Group 2: T3 (migration — depends on T1 + T2)
       |
Group 3 (parallel): T4, T5, T6  ← depends on T1
       |
Group 4: T7  ← depends on nothing (types only, but ship after backend intent is clear)
       |
Group 5 (parallel): T8, T9, T10, T11  ← depend on T7
       |
Group 6: T12  ← i18n (depends on T8/T10 to know what keys are needed)
       |
Group 7: T13  ← test fixup (depends on T4–T11 being done)
```

---

## Group 1 — Backend entity + persistence (parallel)

### T1 — BudgetLine entity: add `Description` property + update domain methods

**Satisfies**: REQ-BLD-01  
**File**: `Project/src/MyBudget.Features/SharedKernel/Entities/BudgetLine.cs`  
**Action**: Modify

- Add `public string? Description { get; private set; }` property.
- Add `string? description = null` parameter to `BudgetLine.Create()` factory method; assign `Description = description`.
- Add `string? description = null` parameter to `BudgetLine.Update()` domain mutator; assign `Description = description`.
- Do NOT add any `Note` property.

**Acceptance criterion**: The entity compiles with `Description` exposed and `Create()`/`Update()` signatures match the design contract.

---

### T2 — EF column configuration for `Description`

**Satisfies**: REQ-BLD-01  
**File**: `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/BudgetLineConfiguration.cs`  
**Action**: Modify

- Add `builder.Property(l => l.Description).HasColumnName("Description").HasMaxLength(500).IsRequired(false)`.
- Do NOT add any `Note` column config.

**Acceptance criterion**: Configuration compiles; EF sees the property with the correct constraints.

---

## Group 2 — EF migration (sequential after T1 + T2)

### T3 — Generate EF migration `AddBudgetLineDescription`

**Satisfies**: REQ-BLD-01 (persistence)  
**Files created**:
- `Project/src/MyBudget.Features/Migrations/{timestamp}_AddBudgetLineDescription.cs`
- `Project/src/MyBudget.Features/Migrations/{timestamp}_AddBudgetLineDescription.Designer.cs`
- `Project/src/MyBudget.Features/Migrations/AppDbContextModelSnapshot.cs` (updated)

**Command**:
```bash
dotnet ef migrations add AddBudgetLineDescription \
  --project Project/src/MyBudget.Features \
  --startup-project Project/src/MyBudget.Api
```

**Action**: Verify the generated `Up()` adds `Description varchar(500) NULL` to `BudgetLines`. Verify `Down()` drops the column. No other tables should be touched.

**Acceptance criterion**: Migration adds exactly one nullable varchar(500) column; snapshot compiles.

---

## Group 3 — Backend slices (parallel, depend on T1)

### T4 — CreateBudgetLine slice: add `description`, remove `note`

**Satisfies**: REQ-BLD-02  
**Files**:
- `Project/src/MyBudget.Features/Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineCommand.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineValidator.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineHandler.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineEndpoint.cs`

**Action**: Modify each file.

- **Command**: Replace `string? Note` (if present) with `string? Description`. If `Note` is absent, only add `Description`.
- **Validator**: Add `RuleFor(x => x.Description).MaximumLength(500)` (fires when not null; FluentValidation applies max-length regardless of null by default — verify existing null handling is consistent).
- **Handler**: Pass `cmd.Description` to `BudgetLine.Create(...)`.
- **Endpoint**: No change needed if the command is record-bound via `[AsParameters]` or body binding; confirm request body picks up `description`.

**Acceptance criterion**: `CreateBudgetLine` command has no `Note` parameter; `description` is accepted and passed through to the entity.

---

### T5 — UpdateBudgetLine slice: add `description`, remove `note`

**Satisfies**: REQ-BLD-03  
**Files**:
- `Project/src/MyBudget.Features/Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineCommand.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineValidator.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineHandler.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineEndpoint.cs`

**Action**: Mirror T4 for the update path.

- **Command**: Replace `string? Note` (if present) with `string? Description`.
- **Validator**: Add `RuleFor(x => x.Description).MaximumLength(500)`.
- **Handler**: Pass `cmd.Description` to `line.Update(...)`. Do NOT touch `BudgetLineRevision`.
- **Endpoint**: Confirm body binding picks up `description`.

**Acceptance criterion**: `UpdateBudgetLine` command has no `Note` parameter; `description` is passed to the domain mutator.

---

### T6 — ListBudgetLines slice: add `Description` to response, remove `Note`

**Satisfies**: REQ-BLD-04  
**Files**:
- `Project/src/MyBudget.Features/Features/BudgetStructure/ListBudgetLines/ListBudgetLinesQuery.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/ListBudgetLines/ListBudgetLinesHandler.cs`

**Action**: Modify.

- **Query/Response record**: Replace `string? Note` with `string? Description` in `BudgetLineResponse` (or the Dapper row record, whichever holds the mapping).
- **Handler SQL**: Add `bl."Description"` to the `SELECT` projection. Remove any `r."Note"` or revision-join projection of `note`. If the revision lateral join exists only to supply `note`, it can stay for other fields; just remove the `note` column from the projection.
- Update any Mapster or manual mapping from the Dapper row to `BudgetLineResponse`.

**Acceptance criterion**: Response record has `Description string?`; no `Note` property; SQL selects `bl."Description"`; no revision `note` in projection.

---

## Group 4 — Frontend types (can start alongside Group 3)

### T7 — Frontend TypeScript types: add `description`, remove `note`

**Satisfies**: REQ-BLD-08  
**File**: `Project/frontend/src/features/budget-structure/types.ts`  
**Action**: Modify.

- `BudgetLineResponse`: remove `note?: string`, add `description?: string`.
- `CreateBudgetLinePayload`: remove `note?: string`, add `description?: string`.
- `UpdateBudgetLinePayload`: remove `note?: string`, add `description?: string`.
- Check the same file (or any composable/store) for any place where `.note` is set on a payload — remove those assignments.

**Acceptance criterion**: TypeScript compilation has no errors; setting `payload.note` on any of the three types is a type error.

---

## Group 5 — Frontend components + views (parallel, depend on T7)

### T8 — BudgetLineModal.vue: replace `note` textarea with `description`

**Satisfies**: REQ-BLD-07  
**File**: `Project/frontend/src/features/budget-structure/components/BudgetLineModal.vue`  
**Action**: Modify.

- In the form reactive state, replace `note: ''` with `description: ''` (or `description: undefined`).
- Replace the note `<textarea>` with a description `<textarea>` bound to `form.description` (`id="line-description"`, `maxlength="500"`).
- Update `handleSubmit` (or equivalent) to send `description` in the payload, not `note`.
- Update the label to use the `budgetLines.description` i18n key (to be added in T12).
- Remove any note-related i18n key usage in this component.

**Acceptance criterion**: Modal has a description textarea with `maxlength="500"`; no note input; form submission payload includes `description`.

---

### T9 — BudgetLineRow.vue: remove inline `note`

**Satisfies**: REQ-BLD-06 (inline row cleanup)  
**File**: `Project/frontend/src/features/budget-structure/components/BudgetLineRow.vue`  
**Action**: Modify.

- Remove any note input or note display from the inline row component.
- If the row currently shows a note field, remove it entirely (the description is shown at the view/table level in T10, not inline in the row).
- Ensure no reference to `form.note` or `payload.note` remains.

**Acceptance criterion**: `BudgetLineRow.vue` has no note input/display; compiles without TypeScript errors.

---

### T10 — BudgetLinesView.vue: add `description` column, remove `note` column

**Satisfies**: REQ-BLD-06  
**File**: `Project/frontend/src/features/budget-structure/views/BudgetLinesView.vue`  
**Action**: Modify.

- Replace the `Note` table column header with `Description` (use `budgetLines.description` i18n key — T12).
- Replace the note cell content with `description` cell content, truncated to ~80–100 chars using CSS `text-overflow: ellipsis` + `overflow: hidden` + `white-space: nowrap` on a fixed-width container (or `line-clamp-2` utility). Safe for unicode/emoji.
- Replace `inlineAddForm.note` with `inlineAddForm.description` in the inline-add row (if the view has one).
- Update the inline-add submit payload accordingly.

**Acceptance criterion**: Table has a `Description` column (truncated); no `Note` column exists; inline-add row (if present) uses `description`.

---

### T11 — BudgetLineCustomizationsView.vue: verify `note` is wired (read-only audit)

**Satisfies**: REQ-BLD-05  
**File**: `Project/frontend/src/features/budget-structure/views/BudgetLineCustomizationsView.vue`  
**Action**: Read and verify.

- Confirm the inline-edit form has a `note` input bound to the `UpdateBudgetLineRevision` payload.
- Confirm the modal form (if separate) has the same.
- Confirm this view does NOT reference `BudgetLine.description` (it deals with revisions only).
- If a gap is found (note input missing), create a separate gap-fix task rather than fixing it here.

**Acceptance criterion**: Audit complete; either "no gap found — no change needed" or a gap-fix task is filed. This change does not widen scope.

---

## Group 6 — i18n (sequential after T8 + T10 to know what keys are in use)

### T12 — i18n: add `description` key, remove BudgetLine-level `note` key

**Satisfies**: REQ-BLD-09  
**Files**:
- `Project/frontend/src/i18n/locales/en.json`
- `Project/frontend/src/i18n/locales/es.json`

**Action**: Modify both files.

- Add `"description": "Description"` under the `budgetLines` namespace in `en.json`.
- Add `"description": "Descripción"` under the `budgetLines` namespace in `es.json`.
- Remove the line-level `note` i18n key from the `budgetLines` namespace in both files **only if** that key is no longer referenced by any component after T8/T9/T10. Do NOT remove revision-level `note` keys (used in `BudgetLineCustomizationsView`).

**Important**: Before deleting any key, grep for its usage to confirm it is no longer referenced. Revision note keys must survive.

**Acceptance criterion**: Both locale files have `budgetLines.description`; line-level note key is removed (if safe); revision note keys are untouched; no `$t(...)` call in any component references a deleted key.

---

## Group 7 — Test fixup (sequential, after Groups 3–6 complete)

### T13 — Update test files: replace `note` assertions with `description`

**Satisfies**: all REQs (non-regression)  
**Files** (audit each — fix only those that reference `note` in BudgetLine context):
- `Project/frontend/src/features/budget-structure/components/__tests__/BudgetLineModal.spec.ts`
- `Project/frontend/src/features/budget-structure/views/__tests__/BudgetLinesView.spec.ts`
- `Project/frontend/src/features/budget-structure/views/__tests__/BudgetLineCustomizationsView.spec.ts` (verify only — should not need changes)
- `Project/tests/MyBudget.Features.Tests/SharedKernel/Entities/BudgetLineEntityTests.cs`
- `Project/tests/MyBudget.Integration.Tests/Features/BudgetStructure/BudgetLineTests.cs`

**Action**: For each file:

1. Search for `.note` / `"note"` / `note:` in the context of `BudgetLine` create/update/list assertions.
2. Replace with `.description` / `"description"` / `description:` as appropriate.
3. Do NOT modify assertions in `BudgetLineRevisionTests.cs` or `BudgetLineCustomizationsView.spec.ts` that reference revision `note` — those are correct.
4. Add any missing `description` assertions if the test previously relied on a `note` value.

**Acceptance criterion**: All existing tests pass; no test references `BudgetLine.note` or `BudgetLineResponse.note`; revision note tests are untouched.

---

## Review Workload Forecast

| Metric | Estimate |
|--------|----------|
| Backend lines changed | ~80–120 (entity + config + 3 slices + migration) |
| Frontend lines changed | ~100–160 (types + 3 components + 2 locale files) |
| Test fixup lines | ~30–60 |
| **Total estimated** | **~210–340 lines** |
| Chained PRs recommended | No — well under 400-line threshold |
| Decision needed | No — single-pr confirmed |

**Risk note**: The migration runs `AddColumn` on `BudgetLines`. All existing rows get `NULL` — safe, no backfill. The only coordination dependency is that T3 (migration) must land before the API can return `description` without a runtime column error, so T3 blocks the backend slice tasks from being deployed (but not from being authored in parallel).

---

## Dependency Graph (summary)

```
T1, T2 (parallel)
  └─> T3 (migration)
T1 ──────────────────┐
                     ├─> T4, T5, T6 (parallel)
T7 (independent) ───────────────────────────┐
                                            ├─> T8, T9, T10, T11 (parallel)
                                                      └─> T12
                                                            └─> T13
```
