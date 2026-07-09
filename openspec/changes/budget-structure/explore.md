# Exploration: budget-structure

**Change**: budget-structure
**Explored**: 2026-07-09
**Status**: complete

---

## Current State

Auth foundation in place: Users, Budgets, BudgetMemberships (4 roles: ReadOnly=10, Operator=20, Admin=30, Owner=40), per-budget RBAC (`budget:read`, `budget:operator`, `budget:admin` policies). No budget-structure tables exist yet. Two migrations: `InitialCreate` (baseline) and `AddAuthTables` (full auth schema). `BudgetAuthorizationHandler` reads `{id}` from route as budgetId — all new nested routes must keep `{id}` as the budget segment.

---

## Entity Model

```
Budget (existing)
  └── Cycle        [BudgetId, Name, StartDate, EndDate, IsActive]
        └── Period [CycleId, Name, PeriodNumber, StartDate, EndDate, IsClosed]

Budget (existing)
  └── CategoryGroup  [BudgetId, Name, DisplayOrder]
        └── Category [CategoryGroupId, Name, DisplayOrder]

Period
  └── BudgetLine          [PeriodId, CategoryGroupId?, CategoryId?, Name, LineType (enum), IsRecurring]
        └── BudgetLineRevision [BudgetLineId, BudgetedAmount (decimal 18,2), Currency ("GTQ"|"USD"), RevisedAt (DateTime), Note?]
```

**LineType enum values:** `Expense`, `LongTermSavings`, `PreventiveSavings`

**Recommended approach: BudgetLine per Period (Option 1)** — matches owner's Excel mental model (one sheet per year, 12 period columns). Cross-period duplication acceptable at MVP scale.

---

## RBAC Rules

| Operation | Minimum Role | Policy |
|---|---|---|
| Create/update/delete Cycle, Period, CategoryGroup, Category, BudgetLine | Admin | `budget:admin` |
| Read any structural entity | ReadOnly | `budget:read` |

Operator role can record executions (future feature) but cannot configure structure.

---

## Slices (13 total)

**Write (EF Core — `budget:admin`):**
1. `CreateCycle` — POST `/api/budgets/{id}/cycles`
2. `UpdateCycle` — PUT `/api/budgets/{id}/cycles/{cycleId}`
3. `CreatePeriod` — POST `/api/budgets/{id}/cycles/{cycleId}/periods`
4. `CreateCategoryGroup` — POST `/api/budgets/{id}/category-groups`
5. `UpdateCategoryGroup` — PUT `/api/budgets/{id}/category-groups/{groupId}`
6. `CreateCategory` — POST `/api/budgets/{id}/category-groups/{groupId}/categories`
7. `CreateBudgetLine` — POST `/api/budgets/{id}/periods/{periodId}/lines`
8. `UpdateBudgetLine` — PUT `/api/budgets/{id}/periods/{periodId}/lines/{lineId}` (auto-creates revision)
9. `DeleteBudgetLine` — DELETE `/api/budgets/{id}/periods/{periodId}/lines/{lineId}`

**Read (Dapper — `budget:read`):**
10. `ListCycles` — GET `/api/budgets/{id}/cycles`
11. `GetCycleDetail` — GET `/api/budgets/{id}/cycles/{cycleId}` (includes periods)
12. `ListCategoryGroups` — GET `/api/budgets/{id}/category-groups` (with nested categories)
13. `ListBudgetLines` — GET `/api/budgets/{id}/periods/{periodId}/lines` (with latest revision amount)

---

## Validation Rules

| Field | Rule |
|---|---|
| Cycle.StartDate | Required, before EndDate |
| Cycle dates | No overlap with other Cycles in same Budget |
| Period dates | Within parent Cycle range; no overlap within same Cycle |
| CategoryGroup.Name | Not empty, max 200, unique per Budget |
| Category.Name | Not empty, max 200, unique per CategoryGroup |
| BudgetLine.Name | Not empty, max 200 |
| BudgetLine.LineType | Expense \| LongTermSavings \| PreventiveSavings |
| BudgetLineRevision.BudgetedAmount | Decimal >= 0, precision 18,2 |
| Currency | "GTQ" \| "USD" |
| BudgetLine in closed Period | Reject create/update/delete |

---

## Risks

1. **BudgetLine duplication for recurring items** — 240 rows/year/budget at MVP scale is fine; future `copy-to-next-period` slice can help bulk editing.
2. **Period IsClosed guard** — must reject edits to lines in closed periods; easy to omit if not explicitly in spec.
3. **EF Core model snapshot version mismatch** — snapshot says `9.0.17`, packages now at `10.*`. Must verify before running `dotnet ef migrations add`.
4. **PR size ~800 lines** — chained PRs required.
5. **Currency** — GTQ/USD stored as string tag only; exchange-rate conversion belongs to `budget-execution` feature, not here.
