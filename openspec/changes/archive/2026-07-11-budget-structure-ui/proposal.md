# Proposal: Budget Structure UI

## Intent

The MyBudget frontend has no way to manage budget structure (Cycles, Periods, CategoryGroups, Categories, BudgetLines) despite a complete 23-endpoint backend. The app also lacks shared layout infrastructure — `App.vue` is a bare `<RouterView>` and `HomeView` has an ad-hoc inline navbar. This change delivers the layout shell, budget selection, and full CRUD UI for all budget structure entities.

## Scope

### In Scope
- **Layout infrastructure**: `AppLayout.vue` (authenticated shell) + `PublicLayout.vue` (login/register/invitation card) with router nesting
- **AppLayout navbar**: budget switcher, context-actions slot via `layoutStore` (Pinia), notification bell (infrastructure only), user dropdown with role badge
- **Budget selection**: auto-redirect for single-membership users; list for multiple
- **Cycle management**: list, create, edit, delete, set active cycle
- **Period management**: list within cycle detail, create, edit, change status, delete
- **CategoryGroup + Category management**: tree view with drag-and-drop reorder (backend `PUT .../order` exists)
- **BudgetLine management**: inline table row creation; double-click opens modal for full edit
- **Navigation model**: two tabs at budget level (Cycles, Categories) + breadcrumb drill-down within each
- **Role-gating**: UI gates by `membership.role` — admin writes for structure entities, operator writes for budget lines, all roles read
- **i18n**: `budgetStructure.*` namespace (EN + ES)
- **Guided empty states**: wizard-style prompts instead of blank screens
- **Scalar UI**: add `app.MapScalarApiReference()` in `Program.cs`
- **vue-i18n `@` bug**: fix 4 locations in `en.json` / `es.json`
- **Login/Register alignment**: daisyUI v5 `form-control` fix + hardcoded "Language" label

### Out of Scope
- BudgetLineRevisions history UI (no list endpoint exists)
- Budget execution / reporting views
- Comparison chart (future `budget-compare` or `budget-execution-ui`)
- Notification backend / real notification sources
- Dark/light theme toggle (existing infrastructure sufficient)

## Capabilities

### New Capabilities
- `budget-structure-ui`: Full frontend CRUD for Cycles, Periods, CategoryGroups, Categories, BudgetLines
- `app-layout`: Shared authenticated/public layout shell with navbar, budget switcher, context-actions, notifications infrastructure

### Modified Capabilities
- `frontend-scaffold`: Router nesting changes (layout wrappers), `App.vue` simplified
- `auth`: Login/Register views moved under `PublicLayout`; i18n `@` bug fix

## Approach

- **Layout**: `src/layouts/AppLayout.vue` + `PublicLayout.vue`. Router wraps authenticated routes under `AppLayout`, public routes under `PublicLayout`.
- **State**: `layoutStore` (page actions, active budget context), `notificationStore` (badge counter + panel), `budgetStructure.store` (cycles, periods, groups, categories, lines).
- **Feature module**: `src/features/budget-structure/` — mirrors existing `src/features/budget/` pattern.
- **API layer**: feature-level API files using `http` singleton. Typed request/response interfaces.
- **Reorder**: drag-and-drop via `vue-draggable-plus`. Calls existing `PUT .../order` endpoints.
- **Role gating**: UI reads `authStore.user.memberships[].role` for active budget. Admin-only actions hidden/disabled for non-admin. Operator-only BudgetLine writes gated similarly.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/src/App.vue` | Modified | Router nesting only |
| `frontend/src/layouts/` | New | `AppLayout.vue`, `PublicLayout.vue` |
| `frontend/src/router/index.ts` | Modified | Nested routes under layout components |
| `frontend/src/features/budget-structure/` | New | Full feature module (views, components, API, store, types) |
| `frontend/src/stores/` | New | `layout.store.ts`, `notification.store.ts` |
| `frontend/src/views/HomeView.vue` | Modified | Removes inline navbar; redirects to budget selection |
| `frontend/src/i18n/locales/en.json` | Modified | `budgetStructure.*` keys + `@` fix |
| `frontend/src/i18n/locales/es.json` | Modified | `budgetStructure.*` keys + `@` fix |
| `frontend/src/views/LoginView.vue` | Modified | Wrapped by `PublicLayout` |
| `frontend/src/views/RegisterView.vue` | Modified | `PublicLayout` + hardcoded label fix |
| `backend/src/MyBudget.Host/Program.cs` | Modified | Add `MapScalarApiReference()` |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Large scope exceeds 400-line PR budget | High | Plan chained PRs: layout infra → budget selection → cycles/periods → categories → budget lines → fixes |
| daisyUI v5 alignment regression | Medium | Browser-confirm during apply; keep CSS changes minimal |
| Drag-and-drop library choice | Low | Evaluate `sortablejs` vs `@vueuse` during design; fallback to manual up/down buttons |
| DateOnly string handling in TypeScript | Low | Establish string-based date utility in shared types; no `new Date()` parsing |
| `Microsoft.OpenApi` GHSA vulnerability for Scalar | Low | Verify resolved package version before wiring `MapScalarApiReference()` |

## Rollback Plan

All changes are additive frontend files + one backend line. Rollback = revert the merge commit(s). Layout routes can be unwound by reverting `router/index.ts` to pre-change state. No database migrations involved.

## Dependencies

- Backend budget-structure endpoints (23 endpoints — already merged and operational)
- `authStore.user.memberships[]` data shape (already available from auth feature)

## Success Criteria

- [x] Authenticated users see AppLayout navbar with budget switcher on all protected routes
- [x] Single-membership users auto-redirect to their budget's structure view
- [x] Full CRUD for Cycles, Periods, CategoryGroups, Categories, BudgetLines via UI
- [x] Drag-and-drop reorder works for CategoryGroups and Categories
- [x] Role-based UI gating: admin-only actions hidden for operator/read-only users
- [x] Empty states show guided setup prompts
- [x] All UI strings available in EN and ES
- [x] vue-i18n `@` bug fixed in 4 locations
- [x] Scalar API explorer accessible at `/scalar/v1`
- [x] Login/Register views render correctly under PublicLayout
