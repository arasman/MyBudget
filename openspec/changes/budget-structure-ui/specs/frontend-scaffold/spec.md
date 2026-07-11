# Delta for frontend-scaffold

## ADDED Requirements

### Requirement: Layout Directory

The `src/` directory MUST contain a `layouts/` subdirectory alongside the existing seven. The `layouts/` directory MUST contain at minimum `AppLayout.vue` and `PublicLayout.vue`.

#### Scenario: layouts/ directory exists after change

- GIVEN the budget-structure-ui change is applied
- WHEN `Project/frontend/src/` is inspected
- THEN a `layouts/` subdirectory is present containing `AppLayout.vue` and `PublicLayout.vue`

---

### Requirement: Feature Module Directory

The `src/` directory MUST contain a `features/` subdirectory. The `features/budget-structure/` module MUST exist and contain at minimum the subdirectories: `views/`, `components/`, `api/`, `store/`, `types/`.

#### Scenario: budget-structure feature module exists

- GIVEN the budget-structure-ui change is applied
- WHEN `Project/frontend/src/features/budget-structure/` is inspected
- THEN the five required subdirectories are present

---

## MODIFIED Requirements

### Requirement: Routing

vue-router MUST be configured with `createWebHistory`. Routes MUST be nested under layout parent components: authenticated routes (beginning with `/budgets`) under `AppLayout`, and public routes (`/login`, `/register`, `/invitations/accept`) under `PublicLayout`. `App.vue` MUST contain only a root `<RouterView>`. The legacy flat `/` and `/login` placeholder routes are superseded by this nested structure.

(Previously: Two flat placeholder routes — `/login` and `/` — with no layout nesting.)

#### Scenario: Root path with single membership redirects

- GIVEN the app is loaded at `/` and the user has one budget membership
- WHEN the router guard runs
- THEN the user is redirected to `/budgets/{budgetId}`

#### Scenario: /login path renders inside PublicLayout

- GIVEN the app is loaded at `/login`
- WHEN the router resolves the path
- THEN `LoginView.vue` content is rendered inside `PublicLayout`

#### Scenario: /budgets/:budgetId renders inside AppLayout

- GIVEN the user is authenticated and navigates to `/budgets/1`
- WHEN the router resolves
- THEN the content renders inside `AppLayout` with the navbar visible
