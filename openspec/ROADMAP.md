# MyBudget — Feature Roadmap

**Last updated**: 2026-07-14 (budget-execution-ui archived)
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

### 5. `budget-structure-patch` ✅ archived 2026-07-11

**What**: Schema and endpoint patch on top of `budget-structure` — closes gaps discovered during design review before `budget-execution` can start.

**Scope in**:
- `Currency` reference table (Id, Code, Name, Symbol) seeded with GTQ/Quetzal/Q, USD/US Dollar/$, EUR/Euro/€
- `GET /budgets/{budgetId}/currencies` — read-only list endpoint
- `Cycle` gains `DefaultCurrencyId` (FK, required), `AlternateCurrencyId` (FK, nullable), `ExchangeRate` (decimal(18,6), nullable); pair rule: both or neither
- ExchangeRate semantics: X DefaultCurrency = 1 AlternateCurrency (e.g., 7.5 GTQ = 1 USD)
- `BudgetLineRevision.Currency varchar(3)` → `CurrencyId FK`; existing revision rows deleted in migration (test data)
- `BudgetLine` gains `DisplayOrder (int)` + `ReorderBudgetLines` endpoint
- Restore endpoints (cascading) for Cycle, CategoryGroup, Category, BudgetLine — each with forward-compat `includeExecutionRecords: bool` no-op param
- `Restore()` method on all soft-deletable entities; parent-deleted guard (409) on direct parent
- 66 implementation tasks completed across 3 chained PRs; 218 tests passing
- Verify: PASS WITH WARNINGS (0 CRITICAL, 3 pre-documented intentional deviations)

**Scope out**: Audit logging (→ `audit-log`), ExecutionRecord restore logic (→ `budget-execution`), Currency management UI.

**SDD artifacts**: `openspec/changes/archive/2026-07-11-budget-structure-patch/` — fully archived with all artifacts and verify report

---

### 6. `audit-log` ✅ archived 2026-07-11

**What**: Cross-cutting audit trail — entity mutations + security events.

**Domain**: Financial app requires traceability — Who/What/When/Where for every mutation (create, update, delete, restore) and security event (login, token, invitation). Foundation for future attack detection and policy enforcement.

**Scope in**:
- `AuditLog` entity: SaveChangesAsync override in AppDbContext, whitelisted budget-domain entities (Budget, Cycle, Period, CategoryGroup, Category, BudgetLine, BudgetLineRevision)
- Actions: Created | Updated | Deleted | Restored; fields: EntityName, EntityId, Action, UserId?, Timestamp, BeforeJson?, AfterJson?, BudgetId?
- `SecurityAuditLog` entity: explicit writes in auth handlers; events: FailedLogin, SuccessfulLogin, AccountRegistered, InvitationAccepted, TokenRefreshed, TokenRevoked, AccountLocked; fields include IpAddress, UserAgent, Details (jsonb?)
- `ICurrentUserService` interface + `HttpContextCurrentUserService` (IHttpContextAccessor → ClaimTypes.NameIdentifier)
- `GET /budgets/{budgetId}/audit-log` — paginated, Owner/Admin only, filters: EntityName, Action, date range
- `GET /budgets/{budgetId}/security-audit-log` — paginated, Owner/Admin only, filtered by budget membership
- `IAuditRetentionPolicy` + `AppSettingsAuditRetentionPolicy` (AuditLog:RetentionDays, default 90)
- Background hosted service for TTL cleanup
- EF migration for both tables

**Scope out**: Frontend audit viewer (→ deferred), real-time alerts (→ MVP B), PasswordChanged event (→ `password-management`), user deletion anonymization (→ deferred).

**SDD artifacts**: `openspec/changes/archive/2026-07-11-audit-log/` — fully archived

---

### 6b. `password-management` ✅ archived 2026-07-13

**What**: Password lifecycle management — recovery, account settings change, forced-change policy, account lockout.

**Domain**: Users need self-service password recovery (email link) and the ability to change their password from account settings. A forced-change policy (configurable interval) strengthens security. This feature also produces the `PasswordChanged` and `AccountLocked` SecurityAuditLog events reserved in `audit-log`.

**Scope in**:
- Password recovery by email (token link, Mailpit in dev); 30-min configurable TTL; BCrypt-hashed token stored in `PasswordResetTokens`
- Change password from User Account Settings (ChangePasswordModal in AppLayout dropdown)
- Forced-change policy: `ForceChangeAfterDays` (default 365); detected at login; blocks JWT issuance; user directed to `/forgot-password?reason=force`
- Account lockout after N failed login attempts (default 5); locked 30 min; cleared on successful reset
- Password history: last 5 hashes (configurable) blocked via `PasswordHistories` table
- `PasswordChanged` + `AccountLocked` SecurityAuditLog events
- `IPasswordPolicyService` / `AppSettingsPasswordPolicyService` — all policy values in `appsettings.json`
- Frontend: ForgotPasswordView, ResetPasswordView, ChangePasswordModal, forgot-password link in LoginView
- 246 unit | 120 integration | 110 Vitest | E2E created (not run against live server)

**Scope out**: OAuth, SSO, MFA (→ MVP B).

**SDD artifacts**: `openspec/changes/archive/2026-07-13-password-management/` — fully archived

---

### 6c. `budget-structure-i18n-patch` ✅ archived 2026-07-11

**What**: Missing i18n keys for Cycle currency fields.

**Domain**: Cycle creation/edit form shows raw key `budgetStructure.cycles.defaultCurrency` instead of a translated label. AlternateCurrency and ExchangeRate fields are also missing translations.

**Scope in**:
- Add i18n keys: `budgetStructure.cycles.defaultCurrency`, `budgetStructure.cycles.alternateCurrency`, `budgetStructure.cycles.exchangeRate` in EN and ES locales
- Verify CycleForm renders labels correctly

**Scope out**: Currency management UI, exchange rate calculation.

---

### 7. `budget-execution` ✅ archived 2026-07-13

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

### 8. `budget-execution-ui` ✅ archived 2026-07-14

**What**: Multi-period budget matrix view — record and review actual spending vs budget per period.

**Scope in**:
- `budget-execution` feature folder: `useBudgetMatrixStore`, 3 composables (`useCurrencyDisplay`, `useMatrixNavigation`, `usePeriodData`), 13 components, `BudgetMatrixView`
- Native HTML `<table>` with CSS sticky left column; 3-period sliding window with prev/next navigation
- Per-period progressive loading with skeleton cells (`loadingPeriods` map)
- Columns per period: Budgeted | Executed | Difference (color-coded green/red)
- Inline CRUD for CategoryGroups, Categories, BudgetLines (edit, delete, restore with cascade)
- Double-click budget line → `BudgetLineModal` (full edit via Teleport)
- `ExecutionListModal`: list + create + edit + delete + restore execution records per line/period
- `MatrixControls`: currency toggle (GTQ/USD client-side conversion), show-deleted toggle, refresh
- `MatrixSummaryRow` footer: totals by LineType (Expense / LongTermSavings / PreventiveSavings)
- Backend patches: cascade soft-delete to BudgetLines in `DeleteCategoryHandler` / `DeleteCategoryGroupHandler`; parent-deleted guard in `RestoreBudgetLineHandler`; `includeDeleted` on `ListCategoryGroups` and `ListBudgetLines`; `categoryGroupId` on `ListCategoryGroupsHandler`
- BudgetTabs nav entry point for matrix view
- 167 Vitest unit tests; 9 Playwright E2E spec files; 0 CRITICAL issues at archive
- Delivery: 6 chained PRs (~325 lines avg)

**Deferred (→ `budget-execution-ui-patch`)**:
- Drag-and-drop reorder for matrix rows (W-02 — currently arrow-button reorder only)
- BudgetLine currency not persisting (frontend sends `currencyCode` string, backend expects `currencyId` Guid)
- Execution record missing fields: `operationDate`, `currency`, `exchangeRate`
- Multi-currency matrix display using per-execution exchange rate

**SDD artifacts**: `openspec/changes/budget-execution-ui/` (explore, proposal, spec, design, tasks)

---

### 8b. `budget-execution-ui-patch` ⏳ planned

**What**: Follow-up fixes and missing fields deferred from `budget-execution-ui`.

**Scope in** *(requires exploration)*:
- Fix BudgetLine currency: map `currencyCode` → `currencyId` Guid on create/update
- Add `operationDate`, `currency`, `exchangeRate` fields to `ExecutionRecordForm`
- Multi-currency matrix: display amounts in selected currency using per-execution exchange rate
- Drag-and-drop reorder for CategoryGroups, Categories, and BudgetLines in matrix view

---

### 9. `multi-budget` ⏳ planned

**What**: Allow each user to own and belong to multiple budgets.

**Domain**: Currently a budget is auto-created on registration and users land directly on it. With multi-budget, users can create additional budgets, switch between them, and receive invitations to multiple budgets.

**Scope in** *(requires full SDD exploration)*:
- Backend: `CreateBudget`, `ListMyBudgets`, `DeleteBudget` slices; remove auto-redirect assumption
- Frontend: BudgetSelector shows all budgets with create option; `/budgets` landing page lists budgets; budget switcher in sidebar
- RBAC: only owners can delete a budget; existing per-budget roles unchanged

**Scope out**: Budget templates, budget cloning, shared budgets beyond the invitation model.

---

### 10. `current-situation` ⏳ planned

**What**: Backend for accounts, funds, balances, and payment methods.

**Domain**: Sheet 3 of the owner's Excel — current balance across bank accounts, funds, credit cards; income projections; multi-period commitments (compromisos); payment method catalog.

**Scope in** *(requires full SDD exploration)*:
- Entities: Account, Fund, PaymentMethod (bank/account/credit card)
- Credit card settlement tracking (cuotas)
- Historial y Situación Actual view: balance per account, period totals, income vs expense summary
- RBAC: `budget:operator` for write, `budget:read` for read

**Scope out**: Project tracking (→ MVP B), installment/debt tracking (→ MVP B).

---

### 11. `current-situation-ui` ⏳ planned

**What**: Frontend for account balances, payment methods, and situation view.

**Scope in** *(requires exploration)*:
- Accounts and funds management panel
- Situación Actual dashboard view
- Credit card settlement view

---

### 12. `dashboard` ⏳ planned

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
