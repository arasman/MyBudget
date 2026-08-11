# Exploration: budget-structure-ui

## Current Stack

- Vue 3.5 (Composition API, `<script setup>`)
- Vite 8 · Pinia 3 · vue-router 4 · vue-i18n v9
- Tailwind CSS v4 + daisyUI v5
- Axios (shared `http` singleton)
- No separate UI component library — daisyUI class utilities on native HTML

## Frontend Architecture

Feature-flat structure (not atomic design). Key directories:
- `src/views/` — top-level route views
- `src/features/budget/` — budget list/create (only existing feature module)
- `src/stores/` — Pinia stores (`auth.store.ts`, `budget.store.ts`)
- `src/i18n/locales/` — flat `en.json`, `es.json`
- `src/router/index.ts` — route definitions
- `src/http.ts` — Axios singleton with JWT interceptor

**Note:** No `src/features/budget-structure/` exists. This change establishes the first full feature module.

## State Management Pattern

Pinia stores. `auth.store.ts` holds user + memberships. `budget.store.ts` holds list + selected budget. Pattern: store calls thin API functions, exposes reactive refs.

## API Client Pattern

`src/http.ts` — Axios instance with `Authorization: Bearer` interceptor.
Feature-level API files call `http.get/post/put/delete` and return typed data.

## i18n Key Conventions

Flat namespace per feature: `auth.login.*`, `auth.register.*`, `budget.*`.
New keys: `budgetStructure.*`.

## Router Conventions

Nested routes with layout wrappers. Auth guards via `router.beforeEach`. New routes: `/budgets/:budgetId` with children for structure entities.

## Backend Endpoint Inventory (23 endpoints)

All under `/api/budgets/{budgetId}/...`

### Cycles (6)
| Method | Path | Response | Policy |
|--------|------|----------|--------|
| POST | `/cycles` | 201 `{ id }` | budget:admin |
| GET | `/cycles` | `CycleListItem[]` | — |
| GET | `/cycles/{cycleId}` | `CycleDetailResponse` | — |
| PUT | `/cycles/{cycleId}` | 204 | budget:admin |
| DELETE | `/cycles/{cycleId}` | 204 | budget:admin |
| PUT | `/active-cycle` | 204 | budget:admin |

`CycleListItem`: `{ id, name, startDate, endDate, isActive, periodCount }`
`CycleDetailResponse`: `{ id, name, startDate, endDate, isActive, periods: PeriodSummary[] }`

### Periods (4)
| Method | Path | Response | Policy |
|--------|------|----------|--------|
| POST | `/cycles/{cycleId}/periods` | 201 `{ id }` | budget:admin |
| PUT | `/cycles/{cycleId}/periods/{periodId}` | 204 | budget:admin |
| PATCH | `/cycles/{cycleId}/periods/{periodId}/status` | 204 | budget:admin |
| DELETE | `/cycles/{cycleId}/periods/{periodId}` | 204 | budget:admin |

### CategoryGroups (5)
| Method | Path | Response | Policy |
|--------|------|----------|--------|
| POST | `/category-groups` | 201 `{ id }` | budget:admin |
| GET | `/category-groups` | `CategoryGroupResponse[]` | — |
| PUT | `/category-groups/{groupId}` | 204 | budget:admin |
| DELETE | `/category-groups/{groupId}` | 204 | budget:admin |
| PUT | `/category-groups/order` | 204 | budget:admin |

`CategoryGroupResponse`: `{ id, name, displayOrder, categories: CategoryItem[] }`

### Categories (4)
| Method | Path | Response | Policy |
|--------|------|----------|--------|
| POST | `/category-groups/{groupId}/categories` | 201 `{ id }` | budget:admin |
| PUT | `/category-groups/{groupId}/categories/{categoryId}` | 204 | budget:admin |
| DELETE | `/category-groups/{groupId}/categories/{categoryId}` | 204 | budget:admin |
| PUT | `/category-groups/{groupId}/categories/order` | 204 | budget:admin |

### BudgetLines (4)
| Method | Path | Response | Policy |
|--------|------|----------|--------|
| POST | `/periods/{periodId}/lines` | 201 `{ id }` | budget:operator |
| GET | `/periods/{periodId}/lines` | `BudgetLineResponse[]` | — |
| PUT | `/periods/{periodId}/lines/{lineId}` | 204 | budget:operator |
| DELETE | `/periods/{periodId}/lines/{lineId}` | 204 | budget:operator |

`BudgetLineResponse`: `{ id, name, lineType, isRecurring, categoryGroupId, categoryId?, budgetedAmount?, currency?, revisedAt?, note? }` — latest revision inline via LATERAL JOIN.

Dates serialized as `"YYYY-MM-DD"` strings (`DateOnly`). `lineType` as string enum name (e.g. `"Expense"`).

### BudgetLineRevisions
No list endpoint — latest revision only, inlined in BudgetLineResponse.

## Deferred Item Findings

### 1. Scalar UI
- Package `Scalar.AspNetCore v2.*` installed
- `AddOpenApi()` + `MapOpenApi()` already wired in `Program.cs`
- **Missing**: `app.MapScalarApiReference()` — one line after `MapOpenApi()`
- Result: `/scalar/v1` will render visual API explorer

### 2. vue-i18n `@` bug
Confirmed 4 locations:
- `en.json:13` — `auth.login.emailPlaceholder`: `"you@example.com"`
- `en.json:23` — `auth.register.emailPlaceholder`: `"you@example.com"`
- `es.json:13` — `auth.login.emailPlaceholder`: `"tu@ejemplo.com"`
- `es.json:23` — `auth.register.emailPlaceholder`: `"tu@ejemplo.com"`
Fix: replace `@` with `{'@'}` at all 4 locations.

### 3. Login/Register alignment
- Both views use `flex items-center justify-center min-h-screen` — card centered
- `form-control` + `space-y-4` controls spacing
- **Hard finding**: `RegisterView.vue:152` has hardcoded `"Language"` label (not i18n)
- **Soft finding**: daisyUI v5 changed `form-control`/`label` defaults; pixel-level alignment needs browser confirmation

## Approaches Considered

| Approach | Description | Verdict |
|----------|-------------|---------|
| A — Feature module + single Pinia store | `src/features/budget-structure/` + one store | **Recommended** |
| B — Composable-only | No Pinia, `use{Entity}()` composables | Cross-component sharing harder |
| C — Per-entity Pinia stores | Separate store per entity | Too much boilerplate for TFM scale |

**Selected: Approach A** — mirrors existing auth.store.ts / http.ts pattern.

## Critical Prerequisite

**Budget selection UI does not exist.** `authStore.user.memberships[]` holds `{ budgetId, budgetName, role }` but `HomeView` ignores it. Budget selection must be implemented in this change — otherwise no structure route is reachable.

## Risks

- No budget selection flow — prerequisite, must be in scope
- No `src/features/` pattern established yet — this change sets the convention
- Role gating needed in UI (admin vs. operator vs. read-only) from `membership.role`
- `DateOnly` → `string` in TypeScript; avoid `new Date()` without explicit parsing
- BudgetLineRevisions list endpoint does not exist
- daisyUI v5 alignment regression requires browser confirmation, not code-only diagnosis
- Scalar GHSA: verify `Microsoft.OpenApi` resolved version before wiring

## Ready for Proposal

Yes.
