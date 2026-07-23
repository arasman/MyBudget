# Proposal: BudgetLine Description Field

## Intent

BudgetLines lack a stable, line-level text field explaining their purpose. The only text field exposed in create/edit forms is `note`, which is semantically a revision-scoped annotation ("why this amount for this period") but is misleadingly surfaced at the line level. This change adds `description` to `BudgetLine` (what the line covers) and removes the dead `note` plumbing from line-level create/edit UI, enforcing a clean semantic split: `description` lives on the line, `note` lives on revisions.

## Scope

### In Scope
- Add nullable `Description` (max 500 chars) to `BudgetLine` entity, EF config, and migration
- Wire `Description` through `CreateBudgetLine` and `UpdateBudgetLine` slices (command, handler, validator)
- Add `Description` to `ListBudgetLines` SQL query and response DTO
- Drop `note` from `ListBudgetLines` response (revision internals stay in customizations view)
- Remove `note` field from `BudgetLineModal`, `BudgetLineRow` inline-edit, and `BudgetLinesView` inline-add
- Remove `note` from `CreateBudgetLinePayload` and `UpdateBudgetLinePayload` frontend types
- Add `Description` column to `BudgetLinesView` table (truncated to ~100 chars, full text in modal)
- Add `description` to frontend payload types and `BudgetLineResponse`
- Add i18n keys for `description` (en/es)
- Update all affected unit and integration tests

### Out of Scope
- Changes to `BudgetLineRevision` entity or `UpdateBudgetLineRevision` slice (already correct)
- Changes to `BudgetLineCustomizationsView` (revision note already editable there)
- Full-text search or filtering by description
- Description history/audit trail

## Capabilities

### New Capabilities
None

### Modified Capabilities
- `budget-structure`: `BudgetLine` entity gains `Description` field; `ListBudgetLines` response drops `note`, adds `description`
- `budget-structure-ui`: `BudgetLinesView` table replaces Note column with Description column; `BudgetLineModal` replaces Note textarea with Description textarea

## Approach

Add `Description` as a nullable string property on `BudgetLine` with a new EF migration. Wire it through existing Create/Update slices and the ListBudgetLines Dapper query. On the frontend, remove all `note` references from line-level components (modal, inline-edit, inline-add) and replace with `description`. The Note column in the table is replaced by Description. Revision notes remain accessible exclusively through the customizations panel.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SharedKernel/Entities/BudgetLine.cs` | Modified | Add `Description` property, update `Create()` and `Update()` |
| `SharedKernel/Persistence/Configurations/BudgetLineConfiguration.cs` | Modified | Add `Description` column config (max 500) |
| `Migrations/` | New | `AddBudgetLineDescription` migration |
| `Features/BudgetStructure/CreateBudgetLine/` | Modified | Add `Description?` to command, handler, validator |
| `Features/BudgetStructure/UpdateBudgetLine/` | Modified | Add `Description?` to command, handler, validator |
| `Features/BudgetStructure/ListBudgetLines/` | Modified | Add `bl."Description"` to SQL, drop `r."Note"`, update response DTO |
| `frontend/.../types.ts` | Modified | Remove `note?` from payloads, add `description?` |
| `frontend/.../BudgetLineModal.vue` | Modified | Remove Note textarea, add Description textarea |
| `frontend/.../BudgetLineRow.vue` | Modified | Remove note inline-edit, add description display/edit |
| `frontend/.../BudgetLinesView.vue` | Modified | Replace Note column with Description column, update inline-add |
| `frontend/src/i18n/locales/{en,es}.json` | Modified | Add `description` keys, keep `note` keys for revisions |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Frontend tests rely on `note` in line payloads | High | Update all test fixtures and assertions systematically |
| Existing rows get NULL description after migration | Low | Nullable column; UI handles empty state gracefully |
| Users expect note on line edit form | Low | Note was a dead field (backend ignored it); no behavioral change |

## Rollback Plan

Revert the migration (`dotnet ef migrations remove` or generate a down migration dropping the `Description` column). Revert frontend and backend code changes. The `note` field removal from ListBudgetLines response is the only breaking contract change — restore the revision join if rolling back.

## Dependencies

- None. All affected slices and components already exist.

## Success Criteria

- [ ] `BudgetLine` entity has `Description` field persisted via EF migration
- [ ] `CreateBudgetLine` and `UpdateBudgetLine` accept and persist `description`
- [ ] `ListBudgetLines` response includes `description`, excludes `note`
- [ ] `BudgetLineModal` shows Description textarea (no Note field)
- [ ] `BudgetLinesView` table shows Description column (truncated ~100 chars)
- [ ] `BudgetLineRow` inline-edit supports `description`, not `note`
- [ ] Revision `note` remains editable in `BudgetLineCustomizationsView` (no regression)
- [ ] All unit and integration tests pass
