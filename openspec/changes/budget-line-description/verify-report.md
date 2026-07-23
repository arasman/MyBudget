# Verify Report: budget-line-description

**Date**: 2026-07-23
**Branch**: feat/budget-line-description
**Status**: PASS WITH WARNINGS
**Verdict**: PASS WITH WARNINGS

## Executive Summary

All 13 core tasks completed; all spec requirements REQ-BLD-01 through REQ-BLD-09 satisfied. 47 frontend test files (386 tests) pass. One CRITICAL bug found in the EXTRA requirement (CreateBudgetLineRevision note not saved to DB — UpdateRevision called after SaveChangesAsync with no second save). One WARNING for description truncation set to 40 chars instead of the spec-specified 80-100 chars.

---

## Findings

### CRITICAL

**EXTRA-NOTE-PERSIST — Note not saved in CreateBudgetLineRevision**
- File: `Project/src/MyBudget.Features/Features/BudgetStructure/CreateBudgetLineRevision/CreateBudgetLineRevisionHandler.cs` line 72-73
- `createdRevision.UpdateRevision(...)` is called AFTER `await _db.SaveChangesAsync(ct)` and there is no second `SaveChangesAsync`. The note field is set on the in-memory entity but never flushed to the database. Every `CreateRevision` call with a non-null note silently discards it.
- **Fix**: move the `UpdateRevision` call before `SaveChangesAsync`, or add a second `SaveChangesAsync` after the note assignment.

### WARNING

**REQ-BLD-06 — Description truncation shorter than spec**
- File: `Project/frontend/src/features/budget-structure/components/BudgetLineRow.vue` line 111
- `truncate(line.description, 40)` — spec says approximately 80-100 visible characters. The current value (40) is more aggressive than required and may make the column less useful.
- Status: functional (unicode-safe, no data corruption), but doesn't match the spec intent. Upgrade or confirm 40 is an accepted implementation choice.

### SUGGESTION

**EC-04 ambiguity — Empty string stored as null**
- `BudgetLine.Create()` and `Update()` both coerce empty string to null (`string.IsNullOrEmpty(description) ? null : description`). This is a valid implementation choice but EC-04 says "document the choice." The spec acknowledges the ambiguity; the code comment does not. Consider a code comment or doc update.

---

## Requirements Checklist

| Req | Status | Evidence |
|-----|--------|---------|
| REQ-BLD-01 | PASS | `BudgetLine.cs`: `string? Description`, `Create(description)`, `Update(description)`; `BudgetLineConfiguration`: `HasMaxLength(500).IsRequired(false)`; Migration `20260723002217_AddBudgetLineDescription` adds `character varying(500) NULL` |
| REQ-BLD-02 | PASS | `CreateBudgetLineCommand`: `string? Description = null`; validator `MaximumLength(500).When(not null)`; handler passes `description: cmd.Description`; endpoint `CreateBudgetLineRequest` has `string? Description` |
| REQ-BLD-03 | PASS | `UpdateBudgetLineCommand`: `string? Description = null`; validator `MaximumLength(500).When(not null)`; handler calls `line.Update(..., cmd.Description)`; no `Note` present |
| REQ-BLD-04 | PASS | `BudgetLineResponse`: `string? Description`; SQL selects `bl."Description"`; no `Note` in query or row type |
| REQ-BLD-05 | PASS | `BudgetLineCustomizationsView.vue`: inline-edit `editingNote` → `UpdateBudgetLineRevision`; inline-create `inlineAddForm.note` → `createRevision`; no reference to `BudgetLine.description` |
| REQ-BLD-06 | WARNING | Description column exists, uses CSS `truncate` class + JS `truncate(text, 40)`. Truncation value is 40 chars, spec says ~80-100. No corruption. |
| REQ-BLD-07 | PASS | `BudgetLineModal.vue`: `<textarea id="line-description" maxlength="500" v-model="form.description">`; no note field present |
| REQ-BLD-08 | PASS | `types.ts`: `BudgetLineResponse` has `description?: string`, no `note`; `CreateBudgetLinePayload` has `description?: string`, no `note`; `UpdateBudgetLinePayload` has `description?: string`, no `note` |
| REQ-BLD-09 | PASS | `en.json`: `budgetLines.description = "Description"`; `es.json`: `budgetLines.description = "Descripción"`; line-level note key removed; customizations note key (`budgetLines.customizations.note`) intact |
| EXTRA-CreateRevision-note (backend) | CRITICAL | Command/endpoint/handler all accept `string? Note`, but note is applied to entity AFTER SaveChangesAsync — never persisted |
| EXTRA-CreateRevision-note (frontend) | PASS | `budgetLines.api.ts`: `CreateRevisionPayload` has `note?: string`; `BudgetLineCustomizationsView.vue` passes `note: inlineAddForm.note` to `createRevision` |
| EXTRA-MonthlyAmount | PASS | `en.json`/`es.json`: `budgetedAmount = "Monthly Amount"` / `"Monto Mensual"`, `customizations.amount = "Monthly Amount"` / `"Monto Mensual"` |
| UpdateBudgetLineRevision note regression | PASS | `BudgetLineCustomizationsView.vue`: `editingNote` correctly passed; `UpdateRevisionPayload` has `note?`; `updateRevision` API call wired correctly |
| BudgetLineRevision.Note unchanged | PASS | `BudgetLineRevision.cs` `Note` property confirmed present; `UpdateRevision(amount, note)` method intact |

---

## Test Results

- Frontend: 47 test files, 386 tests — all PASS
- Backend build: confirmed succeeded per apply-progress (0 errors, 1 pre-existing warning)
- Migration: `20260723002217_AddBudgetLineDescription` listed as Pending (expected — not yet applied to DB)

---

## Artifacts

- Engram: `sdd/budget-line-description/verify-report`
- OpenSpec: `openspec/changes/budget-line-description/verify-report.md`

## Next Recommended

Fix CRITICAL bug (note not persisted in CreateBudgetLineRevision) before archiving. After fix, re-run verify or proceed to `sdd-archive`.
