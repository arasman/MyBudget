# app-layout Specification

## Purpose

Defines the authenticated and public layout shells for MyBudget, including the navbar, budget switcher, context-actions pattern, notification infrastructure, and user dropdown. All authenticated routes render inside `AppLayout`; public routes (login, register, invitation) render inside `PublicLayout`.

---

## Requirements

### Requirement: LAYOUT-1 — Authenticated Layout Shell

The system MUST render `AppLayout.vue` as the parent component for all authenticated routes. `AppLayout` MUST include a top navbar and a `<RouterView>` content area. No authenticated route MAY render content outside this shell.

#### Scenario: Authenticated route renders inside AppLayout

- GIVEN a user is authenticated
- WHEN they navigate to any route under `/budgets/:budgetId`
- THEN the navbar and content area are both visible in the rendered output

---

### Requirement: LAYOUT-2 — Public Layout Shell

The system MUST render `PublicLayout.vue` as the parent component for `/login`, `/register`, `/forgot-password`, `/reset-password`, and `/invitations/accept`. `PublicLayout` MUST render a centered card container with no authenticated navbar. `PublicLayout` MUST include a header bar that renders `LanguageSwitcher` so unauthenticated users can change locale from any public page.

#### Scenario: Login page renders inside PublicLayout

- GIVEN the user navigates to `/login`
- WHEN the route resolves
- THEN a centered card is rendered without an authenticated navbar

#### Scenario: LanguageSwitcher visible on all public pages

- GIVEN the user is on any public route (`/login`, `/register`, `/forgot-password`, `/reset-password`, `/invitations/accept`)
- WHEN the page renders
- THEN `LanguageSwitcher` is visible in the PublicLayout header

---

### Requirement: LAYOUT-3 — Router Nesting

The router MUST nest authenticated routes under `AppLayout` and public routes under `PublicLayout` using vue-router's parent-child route structure. `App.vue` MUST contain only a root `<RouterView>`.

#### Scenario: Router config reflects layout nesting

- GIVEN the router configuration
- WHEN the route tree is inspected
- THEN `/budgets/:budgetId` and its children are children of the `AppLayout` route
- AND `/login`, `/register` are children of the `PublicLayout` route

---

### Requirement: NAV-1 — Budget Switcher

The navbar MUST display a dropdown of all budgets from `authStore.user.memberships`. Selecting a budget MUST navigate to `/budgets/:budgetId`. The active budget name MUST be displayed as the dropdown trigger label.

#### Scenario: Switching budget navigates to correct route

- GIVEN the navbar is visible and the user has two budget memberships
- WHEN the user selects the second budget from the dropdown
- THEN the router navigates to `/budgets/{secondBudgetId}`

#### Scenario: Active budget label matches current route

- GIVEN the user is on `/budgets/42`
- WHEN the navbar renders
- THEN the dropdown trigger displays the name of budget 42

---

### Requirement: NAV-2 — Context Actions Slot

The navbar MUST render a `pageActions` array from `layoutStore`. Each action MUST appear as a button in the navbar's action area. Views MUST be able to register actions by writing to `layoutStore.pageActions`. Actions MUST be cleared when the route changes.

#### Scenario: View registers a context action

- GIVEN a view sets `layoutStore.pageActions = [{ label: 'New Cycle', onClick: fn }]`
- WHEN the navbar renders
- THEN a "New Cycle" button is visible in the navbar action area

#### Scenario: Context actions cleared on route change

- GIVEN a view has registered page actions
- WHEN the user navigates to a different route
- THEN the navbar action area is empty

---

### Requirement: NAV-3 — Notification Bell

The navbar MUST display a bell icon button. When `notificationStore.unreadCount > 0`, a badge with the count MUST be visible on the icon. Clicking the bell MUST toggle a dropdown panel listing notifications from `notificationStore.items`. The empty-state message MUST be rendered using the i18n key `common.noNotifications`. The notification system is infrastructure only — no backend source is wired in this change.

#### Scenario: Badge appears when unread count is nonzero

- GIVEN `notificationStore.unreadCount = 3`
- WHEN the navbar renders
- THEN a badge displaying "3" is visible on the bell icon

#### Scenario: Empty notification panel uses i18n key

- GIVEN `notificationStore.items` is empty
- AND the locale is `"es"`
- WHEN the bell is clicked
- THEN the dropdown panel shows `"Sin notificaciones"` (resolved from `common.noNotifications`)

---

### Requirement: NAV-4 — User Dropdown

The navbar MUST display a user dropdown triggered by the user's initials (derived from `firstName` + `lastName`). The dropdown MUST show the user's role badge for the active budget, a `LanguageSwitcher` control, and a logout action. Clicking logout MUST call `authStore.logout()` and redirect to `/login`.

#### Scenario: Initials derived correctly

- GIVEN a user with `firstName = "Ana"` and `lastName = "López"`
- WHEN the navbar renders
- THEN the dropdown trigger displays "AL"

#### Scenario: LanguageSwitcher visible in user dropdown

- GIVEN the user is authenticated and opens the user dropdown
- WHEN the dropdown is rendered
- THEN `LanguageSwitcher` is visible inside the dropdown

#### Scenario: Logout redirects to login

- GIVEN the user clicks logout in the user dropdown
- WHEN `authStore.logout()` resolves
- THEN the router navigates to `/login`

---

### Requirement: BUDSEL-1 — Budget Auto-Redirect

When a user with exactly one budget membership lands on `/` (home), the system MUST immediately redirect them to `/budgets/:budgetId` for their sole budget. No selection UI is shown.

#### Scenario: Single-membership user is auto-redirected

- GIVEN a user has exactly one budget membership
- WHEN they navigate to `/`
- THEN they are redirected to `/budgets/{their budgetId}` without user interaction

---

### Requirement: BUDSEL-2 — Budget Selection List

When a user has two or more budget memberships and navigates to `/`, the system MUST display a list of their budgets. Selecting a budget MUST navigate to `/budgets/:budgetId`.

#### Scenario: Multi-membership user sees selection list

- GIVEN a user has three budget memberships
- WHEN they navigate to `/`
- THEN a list of three budgets is displayed

#### Scenario: Selecting a budget navigates

- GIVEN the selection list is shown
- WHEN the user clicks on budget "Company Budget"
- THEN the router navigates to `/budgets/{companyBudgetId}`
