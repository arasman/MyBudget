# Exploration: budget-line-description

## Current State

**BudgetLine entity** (`SharedKernel/Entities/BudgetLine.cs`): no `Note` or `Description` field.

**BudgetLineRevision entity** (`SharedKernel/Entities/BudgetLineRevision.cs`): has `Note` (varchar 200, nullable) — correct home for revision-scoped notes.

### Note flow audit

`note` appears in `CreateBudgetLinePayload` and `UpdateBudgetLinePayload` (frontend types) but is a **dead path** — neither `CreateBudgetLineCommand` nor `UpdateBudgetLineCommand` has a `note` parameter. The backend never consumed it. The `BudgetLineModal.vue` and `BudgetLineRow.vue` render a note input that sends data that is silently ignored.

`UpdateBudgetLineRevision` correctly carries `note` and passes it to `revision.UpdateRevision(amount, note)`.

### Customizations note surface

`BudgetLineCustomizationsView.vue` manages revisions. The `UpdateBudgetLineRevision` endpoint accepts `note`. Need to verify the inline/modal forms in this view actually expose the note field.

### ListBudgetLines query

Returns current revision's `note` as part of `BudgetLineResponse`. After the change, `note` will be dropped from `BudgetLineResponse`; `Description` will be added as a `BudgetLine`-level field.

### Latest migration base

`20260722041748_WidenAuditLogActionColumn`

## Files Affected

**Backend:**
- `SharedKernel/Entities/BudgetLine.cs` — add `Description` property + domain method
- `SharedKernel/Persistence/Configurations/BudgetLineConfiguration.cs` — add column config
- `Features/BudgetStructure/CreateBudgetLine/` — add `description` parameter, drop `note`
- `Features/BudgetStructure/UpdateBudgetLine/` — add `description` parameter, drop `note`
- `Features/BudgetStructure/ListBudgetLines/` — add `Description` to response, drop `note`
- New EF migration

**Frontend:**
- `types.ts` — update `BudgetLineResponse`, `CreateBudgetLinePayload`, `UpdateBudgetLinePayload`
- `BudgetLineModal.vue` — replace `note` field with `description` field
- `BudgetLineRow.vue` — remove inline note; possibly show description
- `BudgetLinesView.vue` — add description column to table
- `BudgetLineCustomizationsView.vue` — verify note is shown (no change expected)
- `i18n/locales/en.json` + `es.json` — add description i18n keys, remove note keys for BudgetLine

## Risks

1. `note` removal from frontend payloads requires test assertion updates in `BudgetLineModal.spec.ts`, store specs, and API specs.
2. `BudgetLineRow` inline-edit note input sends to UpdateBudgetLine (already ignored) — must remove to avoid misleading UX.
3. New nullable `Description` column: safe for existing rows (NULL). Dapper query + response must both include it.
4. Max length consistency: 500 chars in EF HasMaxLength, FluentValidation, and frontend `maxlength`.
