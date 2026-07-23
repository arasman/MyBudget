# MyBudget — Feature Roadmap

**Last updated**: 2026-07-23 (budget-line-description archived)
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

### 8b. `budget-execution-ui-patch` ✅ archived 2026-07-15

**What**: Follow-up fixes and missing fields deferred from `budget-execution-ui`.

**Scope in**:

*Budget line maintenance:*
- Group and Category columns added to BudgetLinesView table and inline add/edit row
- Category selector filtered by selected group in inline create/edit
- Currency bug fixed: `BudgetLineModal` now sends `CurrencyId: Guid` correctly
- Sortable columns (Group | Category | Type | Name | Currency | Budgeted Amount | Recurring); default sort Group→Category→Type→Name; reactive re-sort after inline insert

*Matrix view:*
- Drag-and-drop reorder for Groups via `Sortable.create()` (native SortableJS — VueDraggable removed due to flat `<tbody>` layout conflict)
- Summary footer: reordered (Expenses → Preventive Savings → Long-term Savings), renamed to SubTotal, Total row added (`MatrixTotalRow.vue`)
- STATUS_BREAKPOINT fix: `window.getSelection()?.removeAllRanges()` on dblclick in all 3 matrix row components
- In-place name update without full reload (`MatrixSummaryRow`)

*Execution record:*
- Backend: `OperationDate (DateOnly?)` on `ExecutionRecord` + EF Core migration
- Backend: `operationDate` exposed in list/create/update DTOs
- Backend: `currencyId` in `ListBudgetLines` response
- Frontend: `ExecutionRecordForm` — field reorder, currency dropdown, exchange rate input, calculated amount preview, note always required for all entry types
- Frontend: `ExecutionRecordRow` — shows currency code + amount (e.g. "USD 50.00")

**Tests**: 166 Vitest | 284 .NET unit | 137 .NET integration | 51 E2E — all green
**Commit**: `ea3b315` on `main`
**SDD artifacts**: `openspec/changes/archive/2026-07-15-budget-execution-ui-patch/`

**Deferred (→ `budget-execution-multicurrency`)**:
- W-001: `MatrixTotalRow.vue` sums lines directly — refactor to sum 3 SubTotals (safe for MVPA, no 4th LineType)
- S-001: Upgrade `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 pre-existing vulnerability across 4 .NET projects
- S-002: Prune stale i18n key `budgetExecution.form.validation.noteRequired` from `en.json`
- Multi-currency totals: display executed amounts in alternate currency in matrix footer

---

### 8c. `budget-execution-multicurrency` ✅ archived 2026-07-15

**What**: Phase-3 — currency toggle for entire matrix + inline exchange rate edit + UX modal refactor + deferred cleanup from `budget-execution-ui-patch`.

**Scope in**:
- Currency symbol displayed in all matrix cells (line, category, group, summary, total rows)
- GTQ/USD toggle converts entire matrix view via `useCurrencyDisplay.convert()` (display-only, frontend)
- `MatrixControls`: inline exchange rate input (`type=text inputmode=decimal`), watch-immediate sync from store, save-on-blur/Enter, readonly when all periods closed, guard `parsed > 0`
- `MatrixTotalRow` refactored to sum via `subtotalByLineType(periodId, lineType)` getter (W-001)
- `useBudgetMatrixStore`: added `subtotalByLineType` getter + `syncExchangeRate()` action
- `ExecutionRecordForm`: exchange rate > 0 validation when alternate currency selected
- `ExecutionListModal`: mode system (list/edit), pagination (PAGE\_SIZE=10), collapsible add form, fullscreen toggle
- `ExecutionRecordRow`: lifted inline editing to parent modal; DebitNote shows positive, CreditNote shows negative in green
- Backend fix: `Amount * ExchangeRate` (was `Amount / ExchangeRate`) in `ListPeriodExecutionTotalsHandler` — caused wrong netTotal for alternate-currency entries
- SQLitePCLRaw pinned to 3.53.3 in all 4 .csproj files (S-001)
- i18n: removed 2 stale keys, added `exchangeRateRequired` + 5 modal keys in EN and ES (S-002)
- 4 new Vitest spec files; 198 tests passing

**Tests**: 198 Vitest — all green
**Commit**: `4c0ae72` on `main` (merged from `feat/budget-execution-multicurrency`)
**SDD artifacts**: `openspec/changes/archive/2026-07-15-budget-execution-multicurrency/`

---

### 9a. `soft-delete-ux` ✅ archived 2026-07-17

**What**: Soft-delete UX consistency — show-deleted toggles, restore actions, and ephemeral toast feedback across all budget-structure entities.

**Scope in**:
- Ephemeral toast system: `useToastStore` (Pinia) + `AppToast.vue` component, decoupled from notification bell
- Show-deleted toggles per entity (Cycle, Period, CategoryGroup, Category, BudgetLine) in `useBudgetStructureStore`
- Restore actions: `RestorePeriod` backend slice + frontend restore buttons for all 5 entities
- `ListCycles` and `ListPeriods` backend: `includeDeleted` query param support
- Two-step confirm modals for destructive actions (delete + restore with cascade disclosure for Period)
- i18n keys: `ephemeralToast.*` namespace + restore/delete feedback strings in EN and ES
- E2E: two-step confirm flows, multi-budget anchor, ListPeriods endpoint fixes

**Tests**: all green at archive
**SDD artifacts**: `openspec/changes/archive/2026-07-17-soft-delete-ux/`

---

### 9c. `budget-execution-ui-e2e-debt` ✅ archived 2026-07-17

**What**: E2E test debt closure for budget-execution-ui — toast on create/update + shared E2E helpers + UI-level coverage for delete/restore/edit flows.

**Scope in**:
- Phase 1: `ExecutionRecordForm.vue` patched with `toastStore.push` on create and update; i18n keys `budgetExecution.record.createSuccess` + `updateSuccess` added (EN + ES)
- Phase 1: Shared `e2e/helpers/auth.ts` (`loginWithToken` with `refreshToken` injection) extracted from duplicated budget-matrix/budget-structure impls; shared `e2e/helpers/toast.ts` (`expectToast`) extracted; both helpers refactored to delegate
- Phase 2: 3 new `{ page, request }` specs — `execution-ui-crud.spec.ts` (4 tests), `execution-ui-delete-restore.spec.ts` (5 tests incl. closed-period restore edge case), `execution-ui-toast.spec.ts` (4 tests)

**Scope out**: 11 existing `{ request }` API-only specs in `e2e/budget-execution/` (left untouched).

**Tests**: 24/24 E2E passing (13 new + 11 existing); 0 TypeScript errors
**SDD artifacts**: `openspec/changes/archive/2026-07-17-budget-execution-ui-e2e-debt/`

---

### 9b. `budget-structure-ui-e2e-debt` ✅ archived 2026-07-17

**What**: E2E test debt closure for budget-structure-ui — toast audit/fix + soft-delete/restore E2E coverage.

**Scope in**:
- Phase 1: 7 missing toast calls added to 4 view files (CycleListView, CycleDetailView, CategoryTreeView, BudgetLinesView) + 7 i18n keys (updateSuccess, setActiveSuccess, statusSuccess per entity) in EN + ES
- Phase 2: `expectToast(page, text)` helper + 5 `seedDeleted*` API-seed helpers in `helpers.ts`
- Phase 3: Toast assertions retrofitted into all existing CRUD E2E tests (14 assertions across 4 spec files)
- Phase 4: 15 new soft-delete/restore E2E tests — toggle ON/OFF, restore + cascade Period disclosure, success toasts per entity
- Review fixes: broken `data-id` selector (R3-001 CRITICAL), trivial negative assertions (R3-002), slug collision risk (R3-003), toast timeout (R3-004)

**Tests**: 23/23 E2E passing; 313 backend unit + 161 integration; 0 TypeScript errors
**SDD artifacts**: `openspec/changes/archive/2026-07-17-budget-structure-ui-e2e-debt/`

---

### 9. `multi-budget` ✅ archived 2026-07-16

**What**: Allow each user to own and belong to multiple budgets — create, rename, soft-delete, restore.

**Scope in**:
- Backend: `IsDeleted`/`DeletedAt` soft-delete columns on Budget; 4 VSA slices (CreateBudget, RenameBudget, DeleteBudget, RestoreBudget); `budget:owner` policy for delete; `BudgetAuthorizationHandler` JOIN filters deleted budgets (returns 404); `isDeleted` field on `GET /auth/me` memberships
- Frontend: `BudgetSelectionView` — create modal (daisyUI `showModal()`), delete confirm modal, restore, show-deleted toggle, inline rename (double-click / pencil icon), icon actions (List / Pencil / Trash2); `BudgetTabs` back-link `← Budgets` with `?manage=1` redirect bypass; `AppLayout` budget switcher filters deleted budgets and hides on BudgetSelection route; `CycleListView` reactive `budgetId` computed + watch for budget switch without remount; router guard redirects deleted budget URLs to `/`
- E2E: `e2e/budget-management/multi-budget.spec.ts` — 8 tests (create, validate, delete, restore, guard, reload)
- i18n: budget-management keys (EN + ES)

**Scope out**: Budget templates, cloning, shared budgets beyond invitation model.
**Deferred (→ `soft-delete-ux`)**: Confirmation modal + show-deleted toggle + restore in CategoryTree, BudgetLines, CycleDetail.
**Deferred (→ backlog)**: Success toasts for create/delete/restore; E2E debt (budget-structure-ui 16 specs, budget-execution-ui 11 specs).

**Tests**: 306 backend + 211 frontend — all green; 8 E2E passing
**SDD artifacts**: `openspec/changes/archive/2026-07-16-multi-budget/`

---

### 9d. `global-toast-audit` ✅ archived 2026-07-18

**What**: Cross-slice toast audit — close all missing success feedback across every frontend CRUD operation.

**Scope in**:
- `BudgetSelectionView`: wired orphaned `createSuccess` key to `onBudgetCreated` (post-navigation toast); added `renameSuccess` toast to inline rename handler
- `BudgetMatrixView`: injected `useToastStore`; added toasts to `confirmAddGroup`, `confirmAddCategory`, `confirmAddLine`
- `MatrixGroupRow`: added toasts to `saveEdit` (update-name), `doDelete`, `doRestore`
- `MatrixCategoryRow`: added toasts to `saveEdit` (update-name), `doDelete`, `doRestore`
- `MatrixLineRow`: added toasts to `doDelete`, `doRestore`, `handleEditSubmit` (modal-based edit — post-apply fix)
- `ChangePasswordModal`: migrated from `notificationStore` → `toastStore` (consistency fix)
- i18n: 9 new keys in both `en.json` and `es.json` (`budgetMatrix.rows.*`, `budgetStructure.selection.renameSuccess`, `budgetMatrix.rows.updateLineSuccess`)
- Tests: 7 component test files extended + 1 new i18n integration spec (277 Vitest total)

**Deferred**: `InviteUserModal` inline `successMessage` feedback (adequate UX, low priority); error toast audit (→ `global-error-toast-audit` if needed).

**Tests**: 277 Vitest — all green; TypeScript build clean
**SDD artifacts**: `openspec/changes/archive/2026-07-18-global-toast-audit/`

---

### 9e. `input-validation-audit` ✅ archived 2026-07-20

**What**: Full-stack validation hardening — close all missing/incomplete input validations across 7 entities and wire error-toast surfacing for business rule violations.

**Scope in**:
- Backend: name uniqueness checks (including soft-deleted) for Budget (per user), Cycle (per budget), Period (per cycle), BudgetLine (per category-group + category); fixed CategoryGroup/Category uniqueness to use `IgnoreQueryFilters()`; `operationDate` must fall within period date range; note always required (all entry types); BudgetLine amount `> 0`; `BudgetLineRevision.Note HasMaxLength(200)` + EF migration
- Frontend: `store._wrap()` re-throw; `extractApiErrorCode` utility (handles `{ detail }` and `{ error }` shapes); error toasts on all 6 forms for business rule violations; inline validation on all forms (nameRequired, nameTooLong max 200, amountRequired, amountPositive, dateOrder); `operationDate` required + period-range best-effort check; decimal precision (amount max 2dp, exchangeRate max 6dp); CycleListView inline edit validation; 28 new i18n keys (EN + ES)
- E2E/integration test helpers updated: `note` defaults + `operationDate` defaults; `seedBudgetMatrixFixture` period dates widened to 2020–2099
- Delivery: feature-branch-chain (backend PR + frontend PR → tracker → main)

**Tests**: 523 backend unit + integration | 304 frontend unit | 89 E2E — all green
**SDD artifacts**: `openspec/changes/archive/2026-07-20-input-validation-audit/`

---

### 9f. `budget-line-redesign` ✅ archived 2026-07-21

**What**: Promote BudgetLine from Period-scoped to Budget-level with date-range validity and a gapless append-only revision system.

**Domain**: BudgetLine was previously scoped to a Period (PeriodId FK), requiring manual recreation per period and a fragile `IsRecurring` flag. Redesigned to be Budget-level with `StartDate`/`EndDate` validity range. Planned amount tracked via `BudgetLineRevision` with `ValidFrom`/`ValidTo` fields — gapless invariant enforced via `SplitRevision()` domain method.

**Scope in**:
- Entity redesign: removed `PeriodId`, `IsRecurring`, `RevisedAt`; added `StartDate`, `EndDate`, `ValidFrom`, `ValidTo`; `SplitRevision()` domain method (gapless, Edge Case B overwrite)
- `UNIQUE(BudgetId, Name)` — includes soft-deleted rows; no filtered index
- 12 backend slices updated: Create/Update/Delete/Restore/List/ReorderBudgetLines; CreateExecutionRecord (date-range intersection guard `BUDGET_LINE_NOT_IN_PERIOD`); ListPeriodExecutionTotals (ValidFrom LATERAL JOIN for revision amount)
- Frontend: budget-level route `/budgets/:id/lines`; Budget Lines tab in BudgetTabs; `BudgetLineModal` with Amount Revision section (validFrom/validTo/newAmount); inline amount cell read-only (revision requires modal)
- E2E helpers updated to budget-level API routes; per-period List action removed from CycleDetailView
- Delivery: 5 chained PRs (PR1 entity + EF, PR2a BudgetStructure slices, PR2b BudgetExecution slices, PR3 frontend, PR4 integration tests)

**Tests**: 391 unit + 170 integration + 333 frontend unit + 89 E2E = 983 total — all green
**SDD artifacts**: `openspec/archive/budget-line-redesign/`

**Deferred (→ `budget-line-customizations`)**: Separate Customizations view for managing revision date ranges per BudgetLine; backend range guards (BudgetLine date change blocked by active executions, revision delete blocked by active executions); restore validation when execution record falls outside current date range.

---

### 9g. `budget-line-customizations` ✅ archived 2026-07-22

**What**: Revision management UI + backend range guards for BudgetLine date-range integrity.

**Domain**: BudgetLines carry date-range validity and a gapless revision history. Users needed a dedicated view to manage revision splits (amount changes over time), backed by guards to prevent date-range changes that would orphan execution records.

**Scope in**:
- PR1 — `BudgetLineCustomizationsView`: new route `/budgets/:id/lines/:lineId/customizations`; table of all revisions (ValidFrom, ValidTo, Amount, Currency, Note); inline create via `SplitRevision`; delete with confirm modal; edit (amount + note) in-place; toasts for all CRUD; `BudgetTabs` back nav
- PR2a — Domain: `BudgetLineRevision.UpdateRevision()`, `SyncValidFrom()` domain methods; xmin shadow property for EF concurrency
- PR2b — Backend range guards: `UpdateBudgetLineDateRange` rejects changes that would orphan revisions or executions; `DeleteBudgetLineRevision` rejects delete with active executions or original-revision guard
- PR3 — Restore validation: `RestoreExecutionRecord` rejects restore when period falls outside BudgetLine date range (`EXECUTION_OUT_OF_DATE_RANGE`)
- Fix branch — Post-merge UI corrections: `UpdateBudgetLineRevision` PATCH slice (amount + note, allow 0); `BudgetLineModal` read-only fields in edit mode; currency fallback from `store.cycles`; `BudgetLinesView` split date columns, removed breadcrumb; last revision ValidTo shows `line.endDate`; test mocks updated

**Tests**: 386 frontend + 434 unit + 195 integration — all green
**SDD artifacts**: `openspec/archive/budget-line-customizations/` + fix branch `openspec/changes/archive/2026-07-22-budget-line-customizations-fix/`

**Deferred (→ `budget-line-description`)**: `Note` field removed from BudgetLine views (it reflects the active revision's note, not a BudgetLine-level field); replaced with a new optional `Description` field on `BudgetLine` — static per-line descriptor, editable in create/edit modal, visible in the lines table. Requires backend migration + `Description` column on `BudgetLines`.

---

### 9h. `budget-line-description` ✅ archived 2026-07-23

**What**: Add optional `Description` field to `BudgetLine` and remove the misleading `Note` column from BudgetLine views.

**Domain**: The `Note` visible in BudgetLines was projected from `BudgetLineRevision.Note` (the active revision) — a revision-level annotation, not a line-level descriptor. Editing it via the BudgetLine modal or inline edit had no effect (dead path on the backend). This change removes that confusion: `note` stays exclusively in the Customizations view (via `UpdateBudgetLineRevision`), and a new `Description` field on `BudgetLine` serves as a static, optional descriptor of what the line represents.

**Scope in**:
- Backend: `Description varchar(500)?` on `BudgetLines`; EF migration `AddBudgetLineDescription`; wired through `CreateBudgetLine`, `UpdateBudgetLine`, `ListBudgetLines` slices; `note` removed from line-level request records
- `CreateBudgetLineRevision`: `note` wired end-to-end (command, handler, endpoint, frontend) — applied before `SaveChangesAsync`
- Frontend: `description` textarea in `BudgetLineModal`; `description` column in `BudgetLinesView` (truncated 80 chars); inline edit in `BudgetLineRow`; `note` input added to CustomizationsView inline add row
- Label: "Budgeted Amount" → "Monthly Amount" / "Monto Mensual" in BudgetLines and Customizations
- i18n: `budgetStructure.budgetLines.description` key in EN and ES

**Tests**: 386 frontend — all green; backend build clean
**Commit**: `c401834` on `feat/budget-line-description`
**SDD artifacts**: `openspec/changes/archive/2026-07-23-budget-line-description/`

**Deferred (→ `language`)**: Pre-existing i18n gaps across the app (e.g. "Cycles", "Categories" showing in English when locale is ES); language selector UI so users can switch between EN and ES at runtime.

---

### 9i. `language` ⏳ planned

**What**: Full i18n audit + runtime language selector.

**Domain**: Several UI areas still display English strings when the app is in Spanish (e.g. "Cycles", "Categories", other entity labels). No runtime language toggle exists — locale is currently hardcoded. This feature closes the translation debt and adds a language picker.

**Scope in** *(requires SDD exploration)*:
- Audit all i18n namespaces; identify missing/untranslated keys in `es.json`
- Complete Spanish translations for all missing keys
- Language selector component (dropdown or toggle) accessible from app layout / user menu
- Persist selected locale (localStorage or user profile)
- i18n: ensure `en.json` and `es.json` are in full parity

**Scope out**: Additional languages beyond EN/ES, server-side locale detection.

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
