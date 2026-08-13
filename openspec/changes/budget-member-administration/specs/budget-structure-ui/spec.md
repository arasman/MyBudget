# Delta for budget-structure-ui

## MODIFIED Requirements

### Requirement: REQ-NAV-1 — Budget Structure Navigation Tabs

The budget detail view MUST render three tabs when a `cycleId` prop is provided: "Cycles",
"Categories", and "Matrix". When no `cycleId` prop is provided, only "Cycles" and "Categories" tabs
are rendered. The "Matrix" tab MUST link to the `BudgetMatrix` named route using
`{ budgetId, cycleId }` params. The "Matrix" tab MUST have its own active state tracking (not
grouped with `CYCLE_ROUTE_NAMES`). Navigating between tabs MUST NOT lose the active budget context.

When the current user's role for the active budget is `owner` or `admin`
(`useRoleGate(budgetId).isAdmin`), a "Members" tab MUST also be rendered, placed immediately after
"Dashboard" (before "Cycles"), following the same `RouterLink` + `isActive()` pattern as the
existing tabs, and linking to the `BudgetMembers` named route under `/budgets/:budgetId/members`.
When the current user's role is `operator` or `read-only`, the "Members" tab MUST NOT be rendered
at all — entirely absent from the DOM, not merely disabled.

(Previously: `BudgetTabs` rendered "Cycles" / "Categories" / optionally "Matrix" with no
member-role-based tab gating.)

#### Scenario: Default tab on budget entry

- GIVEN the user navigates to `/budgets/:budgetId`
- WHEN the page renders
- THEN the "Cycles" tab is active by default

#### Scenario: Tab switch updates URL

- GIVEN the user is on the Cycles tab
- WHEN they click the "Categories" tab
- THEN the URL reflects the Categories tab route

#### Scenario: Matrix tab renders with cycleId

- GIVEN `BudgetTabs` receives `cycleId="abc-123"`
- WHEN it renders
- THEN three tabs are visible: "Cycles", "Categories", "Matrix"

#### Scenario: Matrix tab absent without cycleId

- GIVEN `BudgetTabs` receives no `cycleId` prop
- WHEN it renders
- THEN only "Cycles" and "Categories" tabs are visible

#### Scenario: Matrix tab active on matrix route

- GIVEN the current route is `BudgetMatrix`
- WHEN `BudgetTabs` renders
- THEN the "Matrix" tab has the active CSS class

#### Scenario: Members tab visible to Owner (WU1)

- GIVEN the caller has `owner` role for the active budget
- WHEN `BudgetTabs` renders
- THEN a "Members" tab is visible, positioned immediately after "Dashboard"

#### Scenario: Members tab visible to Admin (WU1)

- GIVEN the caller has `admin` role for the active budget
- WHEN `BudgetTabs` renders
- THEN a "Members" tab is visible

#### Scenario: Members tab hidden from Operator (WU1)

- GIVEN the caller has `operator` role for the active budget
- WHEN `BudgetTabs` renders
- THEN no "Members" tab element exists in the DOM

#### Scenario: Members tab hidden from ReadOnly (WU1)

- GIVEN the caller has `read-only` role for the active budget
- WHEN `BudgetTabs` renders
- THEN no "Members" tab element exists in the DOM

#### Scenario: Members tab active state (WU1)

- GIVEN the current route is `BudgetMembers`
- WHEN `BudgetTabs` renders
- THEN the "Members" tab has the active CSS class
