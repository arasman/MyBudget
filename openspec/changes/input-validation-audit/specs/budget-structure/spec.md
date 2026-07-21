# Delta for budget-structure

## MODIFIED Requirements

### Requirement: REQ-CG-01: Create CategoryGroup

The system MUST allow creating a CategoryGroup with a Name and DisplayOrder. CategoryGroup.Name
MUST be unique (case-insensitive) per Budget among ALL groups, including soft-deleted ones.
The uniqueness check MUST use `IgnoreQueryFilters()` so that soft-deleted records are included.

(Previously: uniqueness checked only among non-deleted groups — soft-deleted name conflicts produced a DB constraint 500)

#### Scenario: Happy path `@integration`
- GIVEN no CategoryGroup named "Housing" in the budget (active or deleted)
- WHEN POST `/api/budgets/{id}/category-groups` with Name="Housing", DisplayOrder=1
- THEN HTTP 201 with new group id

#### Scenario: Duplicate name rejected — active group `@integration`
- GIVEN an active CategoryGroup named "Housing"
- WHEN POST with Name="Housing"
- THEN HTTP 422 with error code `CATEGORY_GROUP_NAME_DUPLICATE`

#### Scenario: Duplicate name rejected — soft-deleted group `@integration`
- GIVEN a soft-deleted CategoryGroup named "Housing" in the same budget
- WHEN POST with Name="Housing"
- THEN HTTP 422 with error code `CATEGORY_GROUP_NAME_DUPLICATE`

---

### Requirement: REQ-CG-02: Update CategoryGroup

The system MUST allow updating Name and DisplayOrder. Uniqueness rule applies excluding self,
and MUST include soft-deleted records via `IgnoreQueryFilters()`.

(Previously: uniqueness check excluded soft-deleted records)

#### Scenario: Happy path `@integration`
- GIVEN a CategoryGroup "Housing" and no other group named "Home & Utilities" (active or deleted)
- WHEN PUT `.../category-groups/{groupId}` with Name="Home & Utilities"
- THEN HTTP 200 with updated name

#### Scenario: Duplicate name rejected — soft-deleted sibling `@integration`
- GIVEN a soft-deleted CategoryGroup named "Home & Utilities" in the same budget
- WHEN PUT with Name="Home & Utilities" on a different group
- THEN HTTP 422 with error code `CATEGORY_GROUP_NAME_DUPLICATE`

---

### Requirement: REQ-CAT-01: Create Category

The system MUST allow creating a Category under a CategoryGroup. Category.Name MUST be unique
(case-insensitive) within the same CategoryGroup among ALL categories, including soft-deleted ones.
The uniqueness check MUST use `IgnoreQueryFilters()`.

(Previously: uniqueness checked only among non-deleted categories)

#### Scenario: Happy path `@integration`
- GIVEN a CategoryGroup "Housing" with no "Rent" category (active or deleted)
- WHEN POST `.../category-groups/{groupId}/categories` with Name="Rent", DisplayOrder=1
- THEN HTTP 201 with new category id

#### Scenario: Duplicate name within group rejected — soft-deleted `@integration`
- GIVEN a soft-deleted Category "Rent" in the same group
- WHEN POST with Name="Rent"
- THEN HTTP 422 with error code `CATEGORY_NAME_DUPLICATE`

---

### Requirement: REQ-CAT-02: Update Category

The system MUST allow updating Name and DisplayOrder. Uniqueness rule applies within the same
group, excluding self, and MUST include soft-deleted records via `IgnoreQueryFilters()`.

(Previously: uniqueness check excluded soft-deleted records)

#### Scenario: Happy path `@integration`
- GIVEN a Category "Rent" and no soft-deleted or active sibling named "Rent & Mortgage"
- WHEN PUT `.../categories/{categoryId}` with Name="Rent & Mortgage"
- THEN HTTP 200 with updated name

#### Scenario: Duplicate name rejected — soft-deleted sibling `@integration`
- GIVEN a soft-deleted Category "Rent & Mortgage" in the same group
- WHEN PUT with Name="Rent & Mortgage" on a different category
- THEN HTTP 422 with error code `CATEGORY_NAME_DUPLICATE`

---

## ADDED Requirements

### Requirement: REQ-BUDGET-UNIQUE-1: Budget Name Uniqueness per User

The system MUST reject creating or renaming a Budget when the same name already exists for the
same user, including soft-deleted budgets. The check MUST include soft-deleted budgets
(no global `HasQueryFilter` on Budget; handler query MUST NOT add one).

#### Scenario: Create duplicate budget name rejected `@integration`
- GIVEN a budget named "Family Budget" (active) for user U1
- WHEN POST `/api/budgets` with Name="Family Budget" for U1
- THEN HTTP 422 with error code `BUDGET_NAME_DUPLICATE`

#### Scenario: Create rejected when same name in soft-deleted budget `@integration`
- GIVEN a soft-deleted budget named "Family Budget" for user U1
- WHEN POST `/api/budgets` with Name="Family Budget" for U1
- THEN HTTP 422 with error code `BUDGET_NAME_DUPLICATE`

#### Scenario: Rename duplicate budget name rejected `@integration`
- GIVEN budgets "A" and "B" (active) for user U1
- WHEN PATCH/PUT rename on "B" to Name="A"
- THEN HTTP 422 with error code `BUDGET_NAME_DUPLICATE`

#### Scenario: Rename allowed when name is unique `@integration`
- GIVEN only one budget named "A" for user U1
- WHEN rename to "A Updated"
- THEN HTTP 200 with updated name

---

### Requirement: REQ-CYC-NAME-1: Cycle Name Uniqueness per Budget

The system MUST reject creating or updating a Cycle when the same name already exists in the same
budget, including soft-deleted cycles.

#### Scenario: Create duplicate cycle name rejected `@integration`
- GIVEN a Cycle named "2025" (active or soft-deleted) in the budget
- WHEN POST `/api/budgets/{id}/cycles` with Name="2025"
- THEN HTTP 422 with error code `CYCLE_NAME_DUPLICATE`

#### Scenario: Update allowed — self-rename `@integration`
- GIVEN a Cycle "2025" being updated (self)
- WHEN PUT with Name="2025" on the same cycleId
- THEN HTTP 200 (self-exclusion applies)

---

### Requirement: REQ-PER-NAME-1: Period Name Uniqueness per Cycle

The system MUST reject creating or updating a Period when the same name already exists in the same
cycle, including soft-deleted periods.

#### Scenario: Create duplicate period name rejected `@integration`
- GIVEN a Period named "January" (active or soft-deleted) in cycle C1
- WHEN POST `.../cycles/{C1}/periods` with Name="January"
- THEN HTTP 422 with error code `PERIOD_NAME_DUPLICATE`

#### Scenario: Update allowed — self-rename `@integration`
- GIVEN a Period "January" being updated (self)
- WHEN PUT with Name="January" on the same periodId
- THEN HTTP 200 (self-exclusion applies)

---

### Requirement: REQ-BL-NAME-1: BudgetLine Name Uniqueness per (CategoryGroup, Category)

The system MUST reject creating or updating a BudgetLine when the same name already exists within
the same (CategoryGroupId, CategoryId) pair in the same budget period, including soft-deleted lines.

#### Scenario: Create duplicate budget line name rejected `@integration`
- GIVEN a BudgetLine named "Rent" (active or soft-deleted) under (GroupA, CategoryX) in period P1
- WHEN POST `.../periods/{P1}/lines` with Name="Rent", same GroupA and CategoryX
- THEN HTTP 422 with error code `BUDGET_LINE_NAME_DUPLICATE`

#### Scenario: Update allowed — self-rename `@integration`
- GIVEN a BudgetLine "Rent" being updated (self)
- WHEN PUT with Name="Rent" on the same lineId
- THEN HTTP 200 (self-exclusion applies)

---

### Requirement: REQ-BL-AMOUNT-1: BudgetLine Amount Greater Than Zero

The system MUST reject a BudgetLine BudgetedAmount of zero or below. The FluentValidation rule
MUST use `GreaterThan(0)`, not `GreaterThanOrEqualTo(0)`.

(Previously: `GreaterThanOrEqualTo(0)` — allowed zero amounts)

#### Scenario: Amount zero rejected `@unit`
- GIVEN a CreateBudgetLine or UpdateBudgetLine command with BudgetedAmount = 0
- WHEN the validator runs
- THEN HTTP 422 with validation error on BudgetedAmount

#### Scenario: Positive amount accepted `@unit`
- GIVEN BudgetedAmount = 0.01
- WHEN the validator runs
- THEN no validation error on BudgetedAmount

---

### Requirement: REQ-BL-NOTE-MAX-1: BudgetLineRevision Note Max Length

`BudgetLineRevisionConfiguration` MUST configure `Note` with `HasMaxLength(200)`.

#### Scenario: Note within max length stored `@unit`
- GIVEN a BudgetLineRevision Note of exactly 200 characters
- WHEN SaveChangesAsync runs
- THEN no DB truncation or constraint error

#### Scenario: Note exceeding max length rejected at DB level `@unit`
- GIVEN a BudgetLineRevision Note of 201 characters
- WHEN SaveChangesAsync runs
- THEN a DB constraint violation is raised
