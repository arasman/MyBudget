# Apply Progress: budget-line-description

**Status**: done  
**Date**: 2026-07-23  
**Branch**: feat/budget-line-description

## Task Completion

- [x] T1 — `BudgetLine.cs`: added `Description` property (string?, private set); updated `Create()` with optional `description` param (empty string → null); updated `Update()` with optional `description` param
- [x] T2 — `BudgetLineConfiguration.cs`: added `HasColumnName("Description").HasMaxLength(500).IsRequired(false)`
- [x] T3 — Migration `20260723002217_AddBudgetLineDescription` generated; adds `Description character varying(500) NULL` to `BudgetLines`; `Down()` drops column; build succeeded
- [x] T4 — `CreateBudgetLine` slice: added `string? Description = null` to command; added MaximumLength(500) validator rule; handler passes `description: cmd.Description` to `BudgetLine.Create()`
- [x] T5 — `UpdateBudgetLine` slice: added `string? Description = null` to command; added MaximumLength(500) validator rule; handler passes `cmd.Description` to `line.Update()`
- [x] T6 — `ListBudgetLines` slice: removed `r."Note"` from SQL SELECT and lateral join; added `bl."Description"` to SELECT; replaced `Note` with `Description` in `BudgetLineRow` and `BudgetLineResponse`
- [x] T7 — `types.ts`: replaced `note?: string` with `description?: string` in `BudgetLineResponse`, `CreateBudgetLinePayload`, `UpdateBudgetLinePayload`
- [x] T8 — `BudgetLineModal.vue`: replaced note textarea with description textarea (`id="line-description"`, `maxlength="500"`); updated form reactive and handleSubmit payloads
- [x] T9 — `BudgetLineRow.vue`: replaced note input/display with description; updated form reactive, resetForm, onInlineSave
- [x] T10 — `BudgetLinesView.vue`: replaced Note column header with Description; replaced `inlineAddForm.note` with `inlineAddForm.description`; updated openInlineAdd reset and handleInlineAddSave
- [x] T11 — `BudgetLineCustomizationsView.vue`: READ-ONLY AUDIT. `note` is properly wired to `UpdateBudgetLineRevision` payload via `editingNote`. View does not reference `BudgetLine.description`. No gap found — no code change needed.
- [x] T12 — `en.json` + `es.json`: added `"description": "Description"` / `"description": "Descripción"` under `budgetLines`; removed line-level `"note"` key (verified no remaining component references to `budgetStructure.budgetLines.note`; customizations.note key is untouched)
- [x] T13 — Test fixup: updated `BudgetLineModal.spec.ts` i18n mock (`note` → `description`); updated `BudgetLinesView.spec.ts` i18n mock (`note` → `description`); no BudgetLine entity tests reference note; integration tests have one comment-only reference, no assertion changes needed

## Verification

- `dotnet build Project` — succeeded (0 errors, 1 pre-existing warning)
- `dotnet ef migrations list` — migration `20260723002217_AddBudgetLineDescription` listed as Pending
- Migration Up(): `AddColumn<string>(name: "Description", table: "BudgetLines", type: "character varying(500)", maxLength: 500, nullable: true)`
- Migration Down(): `DropColumn(name: "Description", table: "BudgetLines")`

## Files Changed

### Backend
- `Project/src/MyBudget.Features/SharedKernel/Entities/BudgetLine.cs`
- `Project/src/MyBudget.Features/SharedKernel/Persistence/Configurations/BudgetLineConfiguration.cs`
- `Project/src/MyBudget.Features/Migrations/20260723002217_AddBudgetLineDescription.cs` (created)
- `Project/src/MyBudget.Features/Migrations/20260723002217_AddBudgetLineDescription.Designer.cs` (created)
- `Project/src/MyBudget.Features/Migrations/AppDbContextModelSnapshot.cs` (updated)
- `Project/src/MyBudget.Features/Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineCommand.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineValidator.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineHandler.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineCommand.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineValidator.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineHandler.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/ListBudgetLines/ListBudgetLinesQuery.cs`
- `Project/src/MyBudget.Features/Features/BudgetStructure/ListBudgetLines/ListBudgetLinesHandler.cs`

### Frontend
- `Project/frontend/src/features/budget-structure/types.ts`
- `Project/frontend/src/features/budget-structure/components/BudgetLineModal.vue`
- `Project/frontend/src/features/budget-structure/components/BudgetLineRow.vue`
- `Project/frontend/src/features/budget-structure/views/BudgetLinesView.vue`
- `Project/frontend/src/i18n/locales/en.json`
- `Project/frontend/src/i18n/locales/es.json`

### Tests
- `Project/frontend/src/features/budget-structure/components/__tests__/BudgetLineModal.spec.ts`
- `Project/frontend/src/features/budget-structure/views/__tests__/BudgetLinesView.spec.ts`
