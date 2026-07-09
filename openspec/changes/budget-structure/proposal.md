# Proposal: Budget Structure

## Intent

Users currently have no way to define the structural backbone of their budget: cycles (yearly planning periods), periods (months), category groups, categories, or budget lines. Without this, the application is an empty shell after registration. This feature introduces the full CRUD layer for budget-structure entities, matching the owner's Excel mental model of yearly sheets with monthly columns and categorized line items.

## Scope

### In Scope

- 6 new entities: Cycle, Period, CategoryGroup, Category, BudgetLine, BudgetLineRevision
- EF Core migration: `AddBudgetStructureTables`
- 19 VSA slices (15 write + 4 read) — full CRUD for all structural entities
- IsClosed period guard: reject create/update/delete on BudgetLines when `Period.IsClosed = true`
- Cascade delete rules (delete Cycle cascades Periods; delete CategoryGroup cascades Categories)
- BudgetLine per Period model (one BudgetLine row per line per period)
- RBAC: `budget:admin` for writes, `budget:read` for reads (existing policies)
- Backend i18n: `.resx` keys (EN + ES) for all handler/validator messages

### Out of Scope

- Frontend UI (separate `budget-structure-ui` change)
- Currency exchange-rate conversion (belongs to `budget-execution`)
- Copy-to-next-period bulk operation (future convenience feature)
- BudgetLine execution tracking (belongs to `budget-execution`)
- Period auto-generation from Cycle dates

## Capabilities

### New Capabilities

- `budget-structure`: Full CRUD for budget structural entities (Cycle, Period, CategoryGroup, Category, BudgetLine) with revision history and period-closed guards

### Modified Capabilities

- None

## Approach

Follow the 4-file VSA slice pattern (Command/Validator/Handler/Endpoint). Write slices use EF Core via AppDbContext. Read slices use Dapper via ConnectionFactory. All routes nest under `/api/budgets/{id}/...` so the existing `BudgetAuthorizationHandler` resolves the budget context automatically.

BudgetLine updates auto-create a BudgetLineRevision with the new amount, preserving change history. The latest revision represents the current budgeted amount. Delete operations use soft validation (check for children) where appropriate and hard cascades where the domain allows.

### Slice Inventory (23 slices)

| # | Slice | Verb | Route | Auth |
|---|---|---|---|---|
| 1 | CreateCycle | POST | `/api/budgets/{id}/cycles` | budget:admin |
| 2 | UpdateCycle | PUT | `/api/budgets/{id}/cycles/{cycleId}` | budget:admin |
| 3 | DeleteCycle | DELETE | `/api/budgets/{id}/cycles/{cycleId}` | budget:admin |
| 4 | SetActiveCycle | PUT | `/api/budgets/{id}/active-cycle` | budget:admin |
| 5 | CreatePeriod | POST | `/api/budgets/{id}/cycles/{cycleId}/periods` | budget:admin |
| 6 | UpdatePeriod | PUT | `/api/budgets/{id}/cycles/{cycleId}/periods/{periodId}` | budget:admin |
| 7 | SetPeriodStatus | PATCH | `/api/budgets/{id}/cycles/{cycleId}/periods/{periodId}/status` | budget:admin |
| 8 | DeletePeriod | DELETE | `/api/budgets/{id}/cycles/{cycleId}/periods/{periodId}` | budget:admin |
| 9 | CreateCategoryGroup | POST | `/api/budgets/{id}/category-groups` | budget:admin |
| 10 | UpdateCategoryGroup | PUT | `/api/budgets/{id}/category-groups/{groupId}` | budget:admin |
| 11 | DeleteCategoryGroup | DELETE | `/api/budgets/{id}/category-groups/{groupId}` | budget:admin |
| 12 | ReorderCategoryGroups | PUT | `/api/budgets/{id}/category-groups/order` | budget:admin |
| 13 | CreateCategory | POST | `/api/budgets/{id}/category-groups/{groupId}/categories` | budget:admin |
| 14 | UpdateCategory | PUT | `/api/budgets/{id}/category-groups/{groupId}/categories/{categoryId}` | budget:admin |
| 15 | DeleteCategory | DELETE | `/api/budgets/{id}/category-groups/{groupId}/categories/{categoryId}` | budget:admin |
| 16 | ReorderCategories | PUT | `/api/budgets/{id}/category-groups/{groupId}/categories/order` | budget:admin |
| 17 | CreateBudgetLine | POST | `/api/budgets/{id}/periods/{periodId}/lines` | budget:admin |
| 18 | UpdateBudgetLine | PUT | `/api/budgets/{id}/periods/{periodId}/lines/{lineId}` | budget:admin |
| 19 | DeleteBudgetLine | DELETE | `/api/budgets/{id}/periods/{periodId}/lines/{lineId}` | budget:admin |
| 20 | ListCycles | GET | `/api/budgets/{id}/cycles` | budget:read |
| 21 | GetCycleDetail | GET | `/api/budgets/{id}/cycles/{cycleId}` | budget:read |
| 22 | ListCategoryGroups | GET | `/api/budgets/{id}/category-groups` | budget:read |
| 23 | ListBudgetLines | GET | `/api/budgets/{id}/periods/{periodId}/lines` | budget:read |

### Key Constraints

- **IsClosed guard**: CreateBudgetLine, UpdateBudgetLine, DeleteBudgetLine MUST reject when `Period.IsClosed = true` (HTTP 409, error code `PERIOD_CLOSED`)
- **Cycle overlap**: No two Cycles in the same Budget may have overlapping date ranges
- **Period range**: Period dates MUST fall within parent Cycle date range; no overlap within Cycle
- **Unique names**: CategoryGroup.Name unique per Budget; Category.Name unique per CategoryGroup
- **Revision auto-create**: UpdateBudgetLine creates a new BudgetLineRevision row; never updates existing revisions
- **Currency**: String tag ("GTQ" or "USD") stored on BudgetLineRevision; no conversion logic
- **LineType enum**: Expense, LongTermSavings, PreventiveSavings

## Affected Areas

| Area | Impact | Description |
|---|---|---|
| `SharedKernel/Entities/` | New | 6 entity files + LineType enum |
| `SharedKernel/Persistence/AppDbContext.cs` | Modified | Add 6 DbSets |
| `SharedKernel/Persistence/Configurations/` | New | 6 EF config files |
| `Migrations/` | New | AddBudgetStructureTables migration |
| `Features/BudgetStructure/` | New | 19 slice folders (4 files each) |
| `Resources/Features/BudgetStructure/` | New | .resx files (EN + ES) per slice |

## Risks

| Risk | Likelihood | Mitigation |
|---|---|---|
| PR exceeds 400-line budget (~1500 lines for 19 slices) | High | Chain into 3-4 PRs: entities+migration, write slices, read slices, tests |
| EF Core snapshot version mismatch (9.x vs 10.x) | Medium | Verify snapshot before migration; update if needed |
| Cascade delete removes data silently | Low | DeleteCycle/DeletePeriod warn if children exist; require explicit confirmation or block |
| IsClosed guard missed on a slice | Medium | Spec scenarios + integration tests per guarded endpoint |

## Rollback Plan

Revert the feature branch. Drop the `AddBudgetStructureTables` migration. No existing tables or data are modified by this feature — all entities are new.

## Dependencies

- Auth feature (merged) — provides Budget entity, BudgetMembership, RBAC policies
- EF Core migration tooling — must resolve snapshot version before adding migration

## Success Criteria

- [ ] All 19 endpoints return correct HTTP status codes for happy and error paths
- [ ] IsClosed guard rejects writes on closed periods with 409
- [ ] Cycle date overlap validation prevents conflicting cycles
- [ ] UpdateBudgetLine creates a revision row; read endpoints return latest revision amount
- [ ] RBAC enforced: admin+ for writes, read-only+ for reads
- [ ] Integration tests cover all 19 slices
- [ ] Unit tests cover all validators and handlers

## Proposal Question Round

The following assumptions were made based on the exploration and confirmed business decisions. If any are incorrect or incomplete, please flag them before proceeding to spec/design.

1. **Delete behavior for Cycle/Period with children**: Should deleting a Cycle cascade-delete all its Periods (and their BudgetLines/Revisions), or should it be blocked if children exist? Same question for Period with BudgetLines. The proposal assumes cascade delete is acceptable since this is structural setup, not execution data.

2. **Active Cycle enforcement**: The entity model has `IsActive` on Cycle. Should the system enforce that only one Cycle per Budget can be active at a time? If so, should creating/activating a new Cycle auto-deactivate the previous one, or should it be a validation error?

3. **Period closing**: Who can close/reopen a Period? Is it a separate endpoint (e.g., `PUT .../periods/{periodId}/close`) or a field on UpdatePeriod? The proposal assumes it is a field on UpdatePeriod gated by `budget:admin`.

4. **BudgetLine.CategoryId optionality**: The exploration shows CategoryId as optional (`CategoryGroupId?, CategoryId?`). Should a BudgetLine always require a CategoryGroup, or can it exist without any categorization? The proposal assumes CategoryGroupId is required and CategoryId is optional.

5. **DisplayOrder management**: CategoryGroup and Category have DisplayOrder. Should the API accept explicit order values, or should it auto-increment on create? What happens on delete — re-compact order numbers?
