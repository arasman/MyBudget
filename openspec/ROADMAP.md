# MyBudget — Feature Roadmap

**Last updated**: 2026-07-09
**Source**: `AnalisisInicial/` domain analysis + SDD exploration artifacts

---

## How to read this document

Each feature maps to one SDD change (`openspec/changes/{name}/`).
Backend and UI are split into separate changes so each stays under the 400-line PR budget.
Status values: `✅ archived` | `🔄 in progress` | `⏳ planned` | `🔮 MVP B`

---

## MVP A

### 1. `foundation` ✅ archived 2026-07-07

**What**: Full-stack scaffold — zero to running application.

**Scope in**:
- .NET 10 solution with VSA (Vertical Slice Architecture) skeleton
- Vue 3 + Vite + Tailwind v4 + daisyUI + Pinia + vue-router + vue-i18n + Axios + ESLint + Prettier + Vitest
- Docker Compose infrastructure: PostgreSQL, Redis, Mailpit (dev email), Seq (logs), Jaeger (tracing)
- YARP API gateway, SharedKernel types, EF Core baseline migration, User Secrets
- Serilog structured logging, OpenTelemetry tracing setup

**Scope out**: Business logic, auth, any domain entities.

---

### 2. `auth` ✅ archived 2026-07-08

**What**: Authentication and per-budget authorization.

**Scope in**:
- User registration + JWT login (15-min access token, 7-day rotating refresh token)
- Logout, token refresh, get-current-user profile
- Budget auto-created for owner on register
- Email invitation flow (invite by email, accept via token link)
- Per-budget RBAC: 4 roles (Owner=40, Admin=30, Operator=20, ReadOnly=10)
- Policies: `budget:admin`, `budget:operator`, `budget:read`
- `BudgetAuthorizationHandler` — resolves role from `BudgetMemberships` per request (Dapper + IMemoryCache 5 min TTL)
- Frontend: auth Pinia store, Axios 401 interceptor, LoginView, RegisterView, HomeView, AcceptInvitationView, InviteUserModal
- 112 tests (8 unit + 35 integration + 30 Vitest + 9 E2E Playwright)

**Scope out**: Budget CRUD (create/list/delete budgets beyond owner auto-create), password reset, OAuth.

---

### 3. `budget-structure` ✅ archived 2026-07-10

**What**: Backend CRUD for all structural entities of a budget.

**Domain**: The owner manages a family budget modeled as: Cycle (year) → Period (month) → BudgetLine (rubro), structured by CategoryGroup → Category. Each BudgetLine tracks planned amounts via revision history.

**Scope in**:
- 6 new entities: `Cycle`, `Period`, `CategoryGroup`, `Category`, `BudgetLine`, `BudgetLineRevision`
- `LineType` enum: `Expense`, `LongTermSavings`, `PreventiveSavings`
- EF Core migration: `AddBudgetStructureTables`
- 23 VSA slices (19 write + 4 read):
  - Cycles: Create, Update, Delete, SetActiveCycle
  - Periods: Create, Update, SetPeriodStatus (open/close), Delete
  - CategoryGroups: Create, Update, Delete, ReorderCategoryGroups
  - Categories: Create, Update, Delete, ReorderCategories
  - BudgetLines: Create, Update, Delete
  - Reads: ListCycles, GetCycleDetail, ListCategoryGroups, ListBudgetLines (with latest revision)
- Soft delete (cascade logical) + hard delete (sequential, children first)
- Only one active Cycle per Budget at a time (`SetActiveCycle` atomic swap)
- `IsClosed` guard: reject BudgetLine writes on closed periods (HTTP 409 `PERIOD_CLOSED`)
- Revision auto-create on every BudgetLine update (immutable history)
- Reorder via explicit ordered-ID list (full list required)
- RBAC: `budget:admin` writes, `budget:read` reads
- ~113 tests (unit validators + handler logic + integration per endpoint)
- Delivery: 4 chained PRs (~350 lines each)

**Scope out**: Frontend UI (→ `budget-structure-ui`), execution tracking (→ `budget-execution`), currency exchange-rate conversion, period auto-generation from Cycle dates, copy-to-next-period bulk operation.

**SDD artifacts**: `openspec/changes/budget-structure/` (explore, proposal, spec, design, tasks)

---

### 4. `budget-structure-ui` ✅ archived 2026-07-11

**What**: Frontend for all budget structure management.

**Scope in**:
- App layout: `AppLayout` (authenticated) and `PublicLayout` (public) with top navbar, budget switcher, page-actions slot, notification bell infrastructure, user dropdown
- Auth fixes: Login/Register field alignment with daisyUI v5, `@` escape in i18n placeholders, i18n key for language label
- Budget structure CRUD views: CycleListView, CycleDetailView (periods), CategoryTreeView, BudgetLinesView
- Reusable components: BudgetTabs (Cycles/Categories), EmptyState, form modals per entity, BudgetLineRow
- **Inline editing for all entities**: double-click row → inline edit modal or form; Pencil icon opens full modal; `+` button inline creates row
- Icon actions across all views (lucide-vue-next): Pencil (edit), Trash2 (delete), Star (set active), List (view lines), RefreshCw (change status), Check (save), X (cancel)
- Currency constrained to GTQ/USD (select, not free text)
- `LineType` = Expense, LongTermSavings, PreventiveSavings
- Drag-and-drop reorder for CategoryGroups and Categories via `vue-draggable-plus`
- Pinia `budgetStructure.store` for budget structure state; `layoutStore` and `notificationStore` for shared UI
- `useRoleGate` composable for admin/operator/read-only role gating
- Vitest: 88 tests (34 unit + 54 component); Playwright E2E: 16+ tests (5 spec files, deferred execution)
- Scalar API reference at `/scalar/v1`
- i18n: `budgetStructure.*` namespace in EN and ES

**Scope out**: Execution entries (→ `budget-execution-ui`), account/fund management (→ `current-situation-ui`).

---

### 5. `budget-execution` ⏳ planned

**What**: Backend for recording actual spending (ejecución) against budget lines.

**Domain**: Each BudgetLine per Period has a planned amount. Execution tracks actual transactions — when money was spent, from which account, in which currency.

**Scope in** *(requires full SDD exploration)*:
- `Execution` entity: BudgetLineId, Date, Amount, Currency, ExchangeRateToCurrency, AccountId, PaymentMethodId, Note
- Multi-currency: GTQ (Quetzal) + USD; exchange rate stored per execution entry
- RBAC: `budget:operator` for write (operators can record spending), `budget:read` for read
- Period totals: sum of executions vs budgeted amount per line
- Slices: CreateExecution, UpdateExecution, DeleteExecution, ListExecutions (per period/line)
- IsClosed guard inherited: no new executions on closed periods

**Scope out**: Frontend UI (→ `budget-execution-ui`), account balance tracking (→ `current-situation`).

---

### 6. `budget-execution-ui` ⏳ planned

**What**: Frontend for recording and viewing actual spending per budget line.

**Scope in** *(requires exploration)*:
- Inline execution entry within BudgetLinesTable
- Period execution summary: budgeted vs executed, variance
- Currency conversion display (GTQ/USD with rate shown)

---

### 7. `multi-budget` ⏳ planned

**What**: Allow each user to own and belong to multiple budgets.

**Domain**: Currently a budget is auto-created on registration and users land directly on it. With multi-budget, users can create additional budgets, switch between them, and receive invitations to multiple budgets.

**Scope in** *(requires full SDD exploration)*:
- Backend: `CreateBudget`, `ListMyBudgets`, `DeleteBudget` slices; remove auto-redirect assumption
- Frontend: BudgetSelector shows all budgets with create option; `/budgets` landing page lists budgets; budget switcher in sidebar
- RBAC: only owners can delete a budget; existing per-budget roles unchanged

**Scope out**: Budget templates, budget cloning, shared budgets beyond the invitation model.

---

### 8. `current-situation` ⏳ planned

**What**: Backend for accounts, funds, balances, and payment methods.

**Domain**: Sheet 3 of the owner's Excel — current balance across bank accounts, funds, credit cards; income projections; multi-period commitments (compromisos); payment method catalog.

**Scope in** *(requires full SDD exploration)*:
- Entities: Account, Fund, PaymentMethod (bank/account/credit card)
- Credit card settlement tracking (cuotas)
- Historial y Situación Actual view: balance per account, period totals, income vs expense summary
- RBAC: `budget:operator` for write, `budget:read` for read

**Scope out**: Project tracking (→ MVP B), installment/debt tracking (→ MVP B).

---

### 9. `current-situation-ui` ⏳ planned

**What**: Frontend for account balances, payment methods, and situation view.

**Scope in** *(requires exploration)*:
- Accounts and funds management panel
- Situación Actual dashboard view
- Credit card settlement view

---

### 10. `dashboard` ⏳ planned

**What**: Key charts and summary KPIs for the budget.

**Scope in** *(requires exploration)*:
- 2–3 key charts: income vs budgeted vs executed; breakdown by category; period totals over time
- Mostly frontend (Vue + chart library); backend may need aggregate query endpoints

---

## MVP B

> Features below depend on all MVP A features being complete.

### `projects` 🔮

**What**: Construction/home project tracking (Sheet 2 of owner's Excel).

**Scope**: Projects with phases, budgeted amounts per phase, execution entries, file attachments. Charts: project progress, spend vs budget.

---

### `commitments` 🔮

**What**: Recurring multi-period commitments (compromisos).

**Scope**: Recurring obligations that span periods (e.g. annual insurance paid monthly). Links to BudgetLines, tracks payment status.

---

### `installments` 🔮

**What**: Credit card installment and loan tracking (cuotas / deudas).

**Scope**: Installment plans (N payments), debt amortization, credit card installment settlement. Cross-cycle tracking.

---

### `import-export` 🔮

**What**: Data import/export for migration from Excel and backup.

**Scope**: CSV/XLSX import for budget lines; full budget export; possibly Excel template generation matching the owner's current sheets.

---

### `extended-charts` 🔮

**What**: Advanced analytics beyond the MVP A dashboard.

**Scope**: Projects chart, debts chart, cross-cycle/period comparisons, savings goal progress.

---

## Branch and PR conventions

| Convention | Value |
|---|---|
| Feature branch | `feat/{change-name}` |
| SDD artifacts branch | same as feature branch |
| Merge target | `main` (after archive) |
| PR line budget | ≤ 400 changed lines per PR |
| Chained PRs | Yes, when feature exceeds budget |
| Delivery strategy | feature-branch-chain |

## SDD artifact locations

| Artifact | Path |
|---|---|
| Active changes | `openspec/changes/{name}/` |
| Archived changes | `openspec/changes/archive/YYYY-MM-DD-{name}/` |
| This roadmap | `openspec/ROADMAP.md` |
| Project config | `openspec/config.yaml` |
