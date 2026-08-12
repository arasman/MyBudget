# Delta for app-layout

## ADDED Requirements

### Requirement: LAYOUT-4 — Global Footer

The system MUST render a shared `AppFooter` component in both `AppLayout` and `PublicLayout`, inserted by each shell directly (`App.vue` remains only a root `<RouterView>` per LAYOUT-3). `AppFooter` MUST display "© {year} · Powered by ARAS Systems" as plain text, with `{year}` computed at render time. `AppFooter` MUST NOT contain links. The footer MUST inherit each shell's background.

#### Scenario: Footer visible on authenticated view
- GIVEN an authenticated user on any `/budgets/:budgetId` route
- WHEN the page renders
- THEN `AppFooter` is visible showing the current year and no links

#### Scenario: Footer visible on public view
- GIVEN a visitor on `/`, `/login`, or another public route
- WHEN the page renders
- THEN `AppFooter` is visible showing the current year and no links

## MODIFIED Requirements

### Requirement: LAYOUT-2 — Public Layout Shell

The system MUST render `PublicLayout.vue` as the parent component for `/` (anonymous), `/login`, `/register`, `/forgot-password`, `/reset-password`, and `/invitations/accept`. `PublicLayout` MUST render a shared `PublicBackdrop` component behind its content on every route it serves. For `/login`, `/register`, `/forgot-password`, `/reset-password`, and `/invitations/accept`, `PublicLayout` MUST render a centered card container with no authenticated navbar. For `/` (anonymous), `PublicLayout` MUST render the landing page's own full-width content instead of the centered-card container. `PublicLayout` MUST include a header bar that renders `LanguageSwitcher` so unauthenticated users can change locale from any public page.

(Previously: PublicLayout served only /login, /register, /forgot-password, /reset-password, /invitations/accept, all with a centered card container and no shared backdrop.)

#### Scenario: Login page renders inside PublicLayout
- GIVEN the user navigates to `/login`
- WHEN the route resolves
- THEN a centered card is rendered without an authenticated navbar

#### Scenario: LanguageSwitcher visible on all public pages
- GIVEN the user is on any public route (`/`, `/login`, `/register`, `/forgot-password`, `/reset-password`, `/invitations/accept`)
- WHEN the page renders
- THEN `LanguageSwitcher` is visible in the PublicLayout header

#### Scenario: Anonymous root renders landing without card container
- GIVEN an anonymous visitor navigates to `/`
- WHEN the route resolves
- THEN `PublicBackdrop` renders behind full-width landing content, not a centered card

### Requirement: LAYOUT-3 — Router Nesting And Root Route Gate

The router MUST nest authenticated routes under `AppLayout` and public routes under `PublicLayout` using vue-router's parent-child route structure. `App.vue` MUST contain only a root `<RouterView>`. The root path `/` MUST render distinct content by auth state: anonymous visitors see the public landing page inside `PublicLayout`; authenticated users see today's budget selection/auto-redirect behavior inside `AppLayout`. `/` MUST NOT redirect anonymous visitors to `/login`.

(Previously: `/` had no anonymous behavior — the auth guard redirected anonymous visitors to `/login`; only authenticated routes and the fixed public route list were nested.)

#### Scenario: Router config reflects layout nesting
- GIVEN the router configuration
- WHEN the route tree is inspected
- THEN `/budgets/:budgetId` and its children are children of the `AppLayout` route
- AND `/login`, `/register` are children of the `PublicLayout` route

#### Scenario: Anonymous visitor at root sees landing, no redirect
- GIVEN an unauthenticated visitor
- WHEN they navigate to `/`
- THEN the landing page renders inside `PublicLayout` and no redirect to `/login` occurs

#### Scenario: Authenticated visitor at root keeps today's behavior
- GIVEN an authenticated user
- WHEN they navigate to `/`
- THEN `AppLayout` renders and BUDSEL-1/BUDSEL-2 behavior applies exactly as before this change

### Requirement: BUDSEL-1 — Budget Auto-Redirect

When an authenticated user with exactly one budget membership lands on `/`, the system MUST immediately redirect them to `/budgets/:budgetId` for their sole budget. No selection UI is shown. This requirement applies only to authenticated users; anonymous visitors at `/` see the public landing page per LAYOUT-3.

(Previously: not explicitly scoped to authenticated users, since `/` was auth-only before this change.)

#### Scenario: Single-membership user is auto-redirected
- GIVEN an authenticated user has exactly one budget membership
- WHEN they navigate to `/`
- THEN they are redirected to `/budgets/{their budgetId}` without user interaction

### Requirement: BUDSEL-2 — Budget Selection List

When an authenticated user has two or more budget memberships and navigates to `/`, the system MUST display a list of their budgets. Selecting a budget MUST navigate to `/budgets/:budgetId`. This requirement applies only to authenticated users; anonymous visitors at `/` see the public landing page per LAYOUT-3.

(Previously: not explicitly scoped to authenticated users, since `/` was auth-only before this change.)

#### Scenario: Multi-membership user sees selection list
- GIVEN an authenticated user has three budget memberships
- WHEN they navigate to `/`
- THEN a list of three budgets is displayed

#### Scenario: Selecting a budget navigates
- GIVEN the selection list is shown
- WHEN the user clicks on budget "Company Budget"
- THEN the router navigates to `/budgets/{companyBudgetId}`
