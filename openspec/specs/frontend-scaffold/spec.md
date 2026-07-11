# frontend-scaffold Specification

## Purpose

Define the Vue 3 + Vite frontend skeleton for MyBudget, including folder structure, styling setup, HTTP client, internationalisation, routing, linting, and test infrastructure. All subsequent feature slices add to this scaffold without altering its configuration.

## Requirements

### Requirement: Folder Structure

The frontend MUST be located at `Project/frontend/`. The `src/` directory MUST contain the following subdirectories: `components/`, `stores/`, `router/`, `i18n/`, `api/`, `views/`, `types/`, `layouts/`, and `features/`. No source files MAY be placed directly in `src/` except `main.ts` and `App.vue`.

#### Scenario: Expected directories exist after scaffold

- GIVEN the scaffold has been applied
- WHEN the `Project/frontend/src/` directory is inspected
- THEN all nine subdirectories (`components/`, `stores/`, `router/`, `i18n/`, `api/`, `views/`, `types/`, `layouts/`, `features/`) are present

---

### Requirement: Layout Directory

The `src/` directory MUST contain a `layouts/` subdirectory alongside the existing directories. The `layouts/` directory MUST contain at minimum `AppLayout.vue` and `PublicLayout.vue`.

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

### Requirement: Tailwind v4 CSS-Only Configuration

Tailwind CSS v4 MUST be configured exclusively via the `@tailwindcss/vite` Vite plugin. No `tailwind.config.ts` or PostCSS configuration file MAY exist. The main CSS entry point MUST use `@import "tailwindcss"` and `@plugin "daisyui"`. daisyUI v5 MUST provide exactly two themes: `light` and `dark`.

#### Scenario: App renders with daisyUI light theme on first load

- GIVEN the Vite dev server is running
- WHEN a browser opens the app with no stored theme preference
- THEN daisyUI `light` theme styles are applied and no CSS errors appear in the console

#### Scenario: Theme switching applies dark theme

- GIVEN the app is running with `light` theme active
- WHEN `data-theme="dark"` is set on the `<html>` element
- THEN daisyUI `dark` theme tokens are applied without a page reload

---

### Requirement: Path Alias

The `@/` alias MUST resolve to `src/` in both Vite (`vite.config.ts`) and TypeScript (`tsconfig.json`). No relative `../../` imports MAY be used for paths that cross directory boundaries.

#### Scenario: Import using alias resolves correctly

- GIVEN a component imports `@/stores/useAuthStore`
- WHEN `pnpm build` is executed
- THEN the import resolves without error and no `Cannot find module` diagnostic appears

---

### Requirement: Axios HTTP Client

A single Axios instance MUST be created and exported from `src/api/axios.ts`. The instance MUST attach three request interceptors: (1) `X-Correlation-Id` header set to a `uuidv4()` value on every request; (2) `Accept-Language` header set from `localStorage.getItem('locale') ?? 'en'`; (3) `Authorization: Bearer {token}` header when a token exists in the auth store. The base URL MUST be configurable via `VITE_API_BASE_URL` environment variable.

#### Scenario: Correlation ID is unique per request

- GIVEN the Axios instance is configured
- WHEN two consecutive requests are made
- THEN each request carries a distinct `X-Correlation-Id` header value

#### Scenario: Accept-Language reflects stored locale

- GIVEN `localStorage.getItem('locale')` returns `'es'`
- WHEN a request is made via the Axios instance
- THEN the request header `Accept-Language` equals `'es'`

#### Scenario: Authorization header is absent when unauthenticated

- GIVEN no token is stored in the auth store
- WHEN a request is made
- THEN the request does NOT contain an `Authorization` header

---

### Requirement: vue-i18n Internationalisation

vue-i18n MUST be installed and configured with `legacy: false` (Composition API mode). The active locale MUST be initialised from `localStorage.getItem('locale') ?? 'en'`. The fallback locale MUST be `'en'`. Locale message files MUST be stored under `src/i18n/locales/` as `en.json` and `es.json`. Both files MUST contain at minimum the `common.*` and `auth.*` key namespaces with stub values.

#### Scenario: Locale defaults to English when no preference stored

- GIVEN `localStorage` does not contain a `locale` key
- WHEN the app initialises
- THEN `i18n.global.locale.value` equals `'en'`

#### Scenario: Locale switch updates rendered text

- GIVEN the app renders a key from `common.*` namespace
- WHEN the locale is changed to `'es'`
- THEN the rendered text immediately reflects the Spanish translation without a page reload

#### Scenario: Missing key falls back to English

- GIVEN a translation key exists in `en.json` but not in `es.json`
- WHEN the locale is `'es'` and the key is accessed
- THEN the English value is rendered and a console warning is emitted

---

### Requirement: Routing

vue-router MUST be configured with `createWebHistory`. Routes MUST be nested under layout parent components: authenticated routes (beginning with `/budgets`) under `AppLayout`, and public routes (`/login`, `/register`, `/invitations/accept`) under `PublicLayout`. `App.vue` MUST contain only a root `<RouterView>`. The legacy flat `/` and `/login` placeholder routes are superseded by this nested structure.

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

---

### Requirement: ESLint and Prettier Configuration

ESLint MUST use the flat config format (`eslint.config.ts`). The following rules MUST be set to `error`: `vue/no-v-html` and `@typescript-eslint/no-explicit-any`. Prettier MUST be configured via `.prettierrc`. Running `pnpm lint` MUST exit with code 0 on the scaffold codebase.

#### Scenario: ESLint catches v-html usage

- GIVEN a Vue component uses `v-html` directive
- WHEN `pnpm lint` is executed
- THEN ESLint reports a `vue/no-v-html` error and exits with a non-zero code

#### Scenario: ESLint catches explicit any

- GIVEN a TypeScript file contains `: any` type annotation
- WHEN `pnpm lint` is executed
- THEN ESLint reports a `@typescript-eslint/no-explicit-any` error

#### Scenario: Scaffold passes lint clean

- GIVEN no scaffold file contains `v-html` or explicit `any`
- WHEN `pnpm lint` is executed
- THEN ESLint exits with code 0 and zero errors

---

### Requirement: Vitest Test Infrastructure

Vitest MUST be installed and configured via `vitest.config.ts` (or inline in `vite.config.ts`). `pnpm vitest run` MUST exit with code 0 on the scaffold. No test cases are required at scaffold time — the infrastructure MUST be ready for future test additions.

#### Scenario: Vitest reports no failures on clean scaffold

- GIVEN no test files exist beyond the setup file
- WHEN `pnpm vitest run` is executed
- THEN the command exits with code 0 and reports zero failures

---

### Requirement: Package Manager Constraint

`pnpm` MUST be the sole package manager for the frontend. `package.json` MUST contain `"packageManager": "pnpm@..."`. No `package-lock.json` or `yarn.lock` file MAY exist in `Project/frontend/`.

#### Scenario: pnpm install succeeds on clean checkout

- GIVEN no `node_modules/` directory exists
- WHEN `pnpm install` is executed in `Project/frontend/`
- THEN all dependencies are installed and `pnpm-lock.yaml` is the only lockfile present
