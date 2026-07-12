# budget-structure-ui Specification

## Purpose

Defines the full CRUD UI for budget structure entities: Cycles, Periods, CategoryGroups, Categories, and BudgetLines. Covers navigation model, role gating, empty states, drag-and-drop reorder, and i18n.

---

## Requirements

### Requirement: REQ-NAV-1 — Budget Structure Navigation Tabs

The budget detail view MUST render two tabs: "Cycles" and "Categories". The active tab MUST be reflected in the URL. Navigating between tabs MUST NOT lose the active budget context.

#### Scenario: Default tab on budget entry

- GIVEN the user navigates to `/budgets/:budgetId`
- WHEN the page renders
- THEN the "Cycles" tab is active by default

#### Scenario: Tab switch updates URL

- GIVEN the user is on the Cycles tab
- WHEN they click the "Categories" tab
- THEN the URL reflects the Categories tab route

---

### Requirement: REQ-CYC-1 — Cycle List

The system MUST display all cycles for the active budget via `GET /api/budgets/{budgetId}/cycles`. Each row MUST show name, start date, end date, period count, and active status. When a cycle has an alternate currency, the list MUST also display the alternate currency symbol or code. An empty state with a guided prompt MUST be shown when no cycles exist.

#### Scenario: Cycles listed

- GIVEN the budget has two cycles
- WHEN the Cycles tab is active
- THEN both cycles are displayed with name, date range, period count, and active badge

#### Scenario: Empty state shown

- GIVEN the budget has no cycles
- WHEN the Cycles tab is active
- THEN a guided empty-state prompt is shown instead of an empty table

#### Scenario: Alternate currency shown when present

- GIVEN a cycle with alternateCurrency.code="USD"
- WHEN the Cycles tab renders
- THEN the cycle row displays the alternate currency symbol or code

#### Scenario: Alternate currency absent when not set

- GIVEN a cycle with alternateCurrency=null
- WHEN the Cycles tab renders
- THEN no alternate currency indicator is shown for that row

---

### Requirement: REQ-I18N-1 — Budget Structure i18n Keys

All user-visible strings in the budget structure UI MUST use keys under the `budgetStructure.*` namespace in `en.json` and `es.json`. No hardcoded English or Spanish strings MAY appear in template markup. The keys `budgetStructure.cycles.defaultCurrency`, `budgetStructure.cycles.alternateCurrency`, and `budgetStructure.cycles.exchangeRate` MUST be present in both locale files with appropriate translations.

#### Scenario: All budget structure strings are i18n-keyed

- GIVEN the EN and ES locale files
- WHEN all `budgetStructure.*` keys are present
- THEN rendering the budget structure UI in either locale shows fully translated text with no fallback warnings

#### Scenario: Currency i18n keys resolve in English

- GIVEN locale is "en"
- WHEN the cycle form or detail view renders currency labels
- THEN `budgetStructure.cycles.defaultCurrency`, `budgetStructure.cycles.alternateCurrency`, and `budgetStructure.cycles.exchangeRate` each resolve to a non-empty English string

#### Scenario: Currency i18n keys resolve in Spanish

- GIVEN locale is "es"
- WHEN the cycle form or detail view renders currency labels
- THEN the three currency keys each resolve to a non-empty Spanish string

---

### Requirement: REQ-CYC-TYPES-1 — CycleListItem and CycleDetail Type Extensions

`CycleListItem` and `CycleDetail` TypeScript types MUST include optional fields `alternateCurrencyId` (string | null), `exchangeRate` (number | null), and `alternateCurrency` (CurrencyItem | null).

#### Scenario: Type accepts null alternate fields

- GIVEN an API response for a cycle without alternate currency
- WHEN the response is parsed into CycleListItem
- THEN the type accepts alternateCurrencyId=null, exchangeRate=null, alternateCurrency=null without TypeScript error

#### Scenario: Type accepts populated alternate fields

- GIVEN an API response for a cycle with AlternateCurrencyId=USD, ExchangeRate=7.5
- WHEN the response is parsed into CycleListItem
- THEN alternateCurrencyId, exchangeRate, and alternateCurrency fields are accessible and typed correctly

---

### Requirement: REQ-CYC-FORM-1 — CycleForm Alternate Currency Inputs

`CycleForm.vue` MUST include an alternate currency dropdown and an exchange rate numeric input. The alternate currency dropdown MUST use the same currency list source as the default currency dropdown. The exchange rate input MUST only be enabled and visible when an alternate currency is selected. The exchange rate label MUST express direction as "X [defaultCurrencyCode] = 1 [alternateCurrencyCode]" using the currently selected currency codes.

#### Scenario: Exchange rate input hidden when no alternate currency

- GIVEN the CycleForm renders with no alternate currency selected
- WHEN the form is displayed
- THEN the exchange rate input is not visible or is disabled

#### Scenario: Exchange rate input shown when alternate currency selected

- GIVEN the user selects an alternate currency in the dropdown
- WHEN the form updates
- THEN the exchange rate input becomes visible and enabled
- AND the label reads "X [defaultCode] = 1 [alternateCode]"

#### Scenario: Exchange rate label reflects selected currencies

- GIVEN defaultCurrency=GTQ and alternateCurrency=USD selected
- WHEN the exchange rate label renders
- THEN the label reads "X GTQ = 1 USD" (or equivalent localized pattern)

---

### Requirement: REQ-CYC-FORM-2 — CycleForm Pair Validation

`CycleForm.vue` MUST enforce client-side pair validation: `alternateCurrencyId` and `exchangeRate` MUST both be filled or both empty. Submitting with only one field filled MUST be prevented with an inline validation message.

#### Scenario: Only alternate currency filled — submission blocked

- GIVEN the user selects an alternate currency but leaves exchange rate empty
- WHEN they attempt to submit the form
- THEN submission is blocked and an inline error is shown for the exchange rate field

#### Scenario: Only exchange rate filled — submission blocked

- GIVEN the user enters an exchange rate but leaves alternate currency unselected
- WHEN they attempt to submit the form
- THEN submission is blocked and an inline error is shown for the alternate currency field

#### Scenario: Both fields filled — submission allowed

- GIVEN the user has selected an alternate currency and entered a positive exchange rate
- WHEN they submit the form
- THEN no pair validation error is raised and the form submits

#### Scenario: Both fields empty — submission allowed

- GIVEN the user has left both alternate currency and exchange rate empty
- WHEN they submit the form
- THEN no pair validation error is raised

---

### Requirement: REQ-CYC-DETAIL-1 — CycleDetailView Alternate Currency Display

`CycleDetailView.vue` MUST display the cycle's alternate currency info and exchange rate when present. The exchange rate display MUST reflect direction semantics in the format "X [defaultCurrencyCode] = 1 [alternateCurrencyCode]".

#### Scenario: Alternate currency section shown when present

- GIVEN a cycle with alternateCurrency.code="USD" and exchangeRate=7.5 and defaultCurrency.code="GTQ"
- WHEN the cycle detail view renders
- THEN the alternate currency section is visible and shows "7.5 GTQ = 1 USD"

#### Scenario: Alternate currency section absent when not set

- GIVEN a cycle with alternateCurrency=null
- WHEN the cycle detail view renders
- THEN no alternate currency section or exchange rate is displayed

---

### Requirement: REQ-CYC-2 — Cycle Create (admin only)

An admin user MUST see a "New Cycle" button registered as a context action via `layoutStore.pageActions`. Clicking it MUST open a form/modal to submit `POST /api/budgets/{budgetId}/cycles`. Non-admin users MUST NOT see the button.

#### Scenario: Admin creates a cycle

- GIVEN the user has `admin` role and the Cycles tab is active
- WHEN they click "New Cycle" and submit valid name, startDate, endDate
- THEN `POST /cycles` is called and the new cycle appears in the list

#### Scenario: Non-admin sees no create button

- GIVEN the user has `operator` role
- WHEN the Cycles tab renders
- THEN the "New Cycle" button is not present in the navbar action area

---

### Requirement: REQ-CYC-3 — Cycle Edit (admin only)

An admin user MUST be able to edit cycle name and dates. The edit action MUST call `PUT /api/budgets/{budgetId}/cycles/{cycleId}`. Non-admin users MUST NOT see edit controls.

#### Scenario: Admin edits cycle name

- GIVEN an admin user and an existing cycle
- WHEN they open the edit form and change the name
- THEN `PUT /cycles/{cycleId}` is called with the updated name

---

### Requirement: REQ-CYC-4 — Cycle Delete (admin only)

An admin user MUST be able to delete a cycle. The action MUST call `DELETE /api/budgets/{budgetId}/cycles/{cycleId}`. A confirmation prompt MUST be shown before deletion.

#### Scenario: Admin deletes a cycle with confirmation

- GIVEN an admin user and a cycle in the list
- WHEN they click delete and confirm the prompt
- THEN `DELETE /cycles/{cycleId}` is called and the cycle is removed from the list

#### Scenario: Cancelling confirmation retains the cycle

- GIVEN an admin user clicks delete
- WHEN they dismiss the confirmation prompt
- THEN the cycle remains in the list and no API call is made

---

### Requirement: REQ-CYC-5 — Set Active Cycle (admin only)

An admin user MUST be able to mark a cycle as active via `PUT /api/budgets/{budgetId}/active-cycle`. Only one cycle MAY be active at a time; the UI MUST reflect the active state with a badge.

#### Scenario: Admin activates a cycle

- GIVEN the admin clicks "Set Active" on an inactive cycle
- WHEN the action completes
- THEN `PUT /active-cycle` is called and the active badge moves to the selected cycle

---

### Requirement: REQ-PER-1 — Period List within Cycle Detail

Clicking a cycle MUST navigate to a cycle detail view with a breadcrumb (Budget > Cycles > [Cycle name]). The detail view MUST list all periods from `CycleDetailResponse.periods`.

#### Scenario: Cycle detail shows breadcrumb and periods

- GIVEN a cycle with three periods
- WHEN the user clicks on that cycle
- THEN the cycle detail view shows the breadcrumb and lists three periods

---

### Requirement: REQ-PER-2 — Period Create (admin only)

An admin user MUST be able to create a period via `POST /api/budgets/{budgetId}/cycles/{cycleId}/periods`. The form MUST collect name, startDate, endDate.

#### Scenario: Admin creates a period

- GIVEN an admin user is on the cycle detail view
- WHEN they submit the new period form with valid data
- THEN `POST /cycles/{cycleId}/periods` is called and the period appears in the list

---

### Requirement: REQ-PER-3 — Period Edit (admin only)

An admin user MUST be able to edit a period's name and dates via `PUT /api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}`.

#### Scenario: Admin edits a period

- GIVEN an admin user on cycle detail
- WHEN they edit the period name and submit
- THEN `PUT /cycles/{cycleId}/periods/{periodId}` is called with updated data

---

### Requirement: REQ-PER-4 — Period Status Change (admin only)

An admin user MUST be able to change a period's status via `PATCH /api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}/status`.

#### Scenario: Admin changes period status

- GIVEN an admin user and a period with status "Draft"
- WHEN they change the status to "Open"
- THEN `PATCH /cycles/{cycleId}/periods/{periodId}/status` is called

---

### Requirement: REQ-PER-5 — Period Delete (admin only)

An admin user MUST be able to delete a period via `DELETE /api/budgets/{budgetId}/cycles/{cycleId}/periods/{periodId}`. A confirmation prompt MUST be shown before deletion.

#### Scenario: Admin deletes a period

- GIVEN an admin user confirms period deletion
- WHEN the action completes
- THEN `DELETE /cycles/{cycleId}/periods/{periodId}` is called and the period is removed

---

### Requirement: REQ-CAT-1 — CategoryGroup and Category Tree

The Categories tab MUST display all category groups with their nested categories via `GET /api/budgets/{budgetId}/category-groups`. Groups MUST be sorted by `displayOrder`. An empty state with a guided prompt MUST be shown when no groups exist.

#### Scenario: Tree renders with groups and categories

- GIVEN the budget has two groups each with two categories
- WHEN the Categories tab is active
- THEN two groups are shown, each expanded to reveal their two categories

---

### Requirement: REQ-CAT-2 — CategoryGroup CRUD (admin only)

An admin user MUST be able to create (`POST /category-groups`), edit (`PUT /category-groups/{groupId}`), and delete (`DELETE /category-groups/{groupId}`) category groups. Non-admin users MUST NOT see write controls.

#### Scenario: Admin creates a category group

- GIVEN an admin user on the Categories tab
- WHEN they submit the new group form
- THEN `POST /category-groups` is called and the group appears in the tree

#### Scenario: Admin deletes a category group with confirmation

- GIVEN an admin user confirms group deletion
- THEN `DELETE /category-groups/{groupId}` is called and the group is removed

---

### Requirement: REQ-CAT-3 — CategoryGroup Drag-and-Drop Reorder (admin only)

An admin user MUST be able to reorder category groups by dragging. On drop, the system MUST call `PUT /api/budgets/{budgetId}/category-groups/order` with the new order. Non-admin users MUST NOT be able to drag.

#### Scenario: Admin reorders category groups

- GIVEN two category groups displayed in order [A, B]
- WHEN an admin drags group B above group A and drops
- THEN `PUT /category-groups/order` is called with the new sequence
- AND the UI reflects the new order [B, A]

---

### Requirement: REQ-CAT-4 — Category CRUD (admin only)

An admin user MUST be able to create (`POST /category-groups/{groupId}/categories`), edit (`PUT /.../{categoryId}`), and delete (`DELETE /.../{categoryId}`) categories within a group.

#### Scenario: Admin creates a category

- GIVEN an admin user on the Categories tab
- WHEN they submit the new category form under a group
- THEN `POST /category-groups/{groupId}/categories` is called

---

### Requirement: REQ-CAT-5 — Category Drag-and-Drop Reorder (admin only)

An admin user MUST be able to reorder categories within a group. On drop, the system MUST call `PUT /api/budgets/{budgetId}/category-groups/{groupId}/categories/order`.

#### Scenario: Admin reorders categories within a group

- GIVEN categories [X, Y] in a group
- WHEN an admin drags Y above X and drops
- THEN `PUT /category-groups/{groupId}/categories/order` is called with the new sequence

---

### Requirement: REQ-BL-1 — BudgetLine List

Selecting a period (accessible from cycle detail) MUST navigate to a budget line view that loads all lines via `GET /api/budgets/{budgetId}/periods/{periodId}/lines`. The view MUST display name, lineType, isRecurring, categoryGroup, category, budgetedAmount, and currency.

#### Scenario: Budget lines listed for a period

- GIVEN a period with three budget lines
- WHEN the user navigates to the budget line view for that period
- THEN three rows are displayed with the correct fields

---

### Requirement: REQ-BL-2 — Inline BudgetLine Creation (operator+)

The budget line view MUST provide an inline empty row at the bottom of the table. Users with at least `operator` role MUST be able to fill in the row and submit to create a line via `POST /api/budgets/{budgetId}/periods/{periodId}/lines`. Read-only users MUST NOT see the inline row.

#### Scenario: Operator creates a budget line inline

- GIVEN an operator user on the budget line view
- WHEN they fill in the inline row and press enter/submit
- THEN `POST /periods/{periodId}/lines` is called and the new line appears in the table

#### Scenario: Read-only user sees no inline row

- GIVEN a user with `read-only` role
- WHEN the budget line view renders
- THEN no inline creation row is visible

---

### Requirement: REQ-BL-3 — BudgetLine Edit via Modal (operator+)

Double-clicking a budget line row MUST open a full edit modal. Submitting the modal MUST call `PUT /api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}`. Read-only users MUST NOT be able to open the edit modal.

#### Scenario: Operator double-clicks to edit

- GIVEN an operator user and a budget line row
- WHEN they double-click the row
- THEN an edit modal opens pre-populated with the line's current values

#### Scenario: Operator submits edit modal

- GIVEN the edit modal is open with changes
- WHEN the operator submits
- THEN `PUT /periods/{periodId}/lines/{lineId}` is called and the row reflects the update

---

### Requirement: REQ-BL-4 — BudgetLine Delete (operator+)

Users with at least `operator` role MUST be able to delete a budget line via `DELETE /api/budgets/{budgetId}/periods/{periodId}/lines/{lineId}`. A confirmation prompt MUST be shown.

#### Scenario: Operator deletes a budget line

- GIVEN an operator user confirms budget line deletion
- THEN `DELETE /periods/{periodId}/lines/{lineId}` is called and the row is removed

---

### Requirement: REQ-I18N-1 — Budget Structure i18n Keys

All user-visible strings in the budget structure UI MUST use keys under the `budgetStructure.*` namespace in `en.json` and `es.json`. No hardcoded English or Spanish strings MAY appear in template markup.

#### Scenario: All budget structure strings are i18n-keyed

- GIVEN the EN and ES locale files
- WHEN all `budgetStructure.*` keys are present
- THEN rendering the budget structure UI in either locale shows fully translated text with no fallback warnings

---

### Requirement: REQ-FIX-1 — Scalar API Reference Endpoint

The backend MUST expose the Scalar API explorer at `/scalar/v1` by wiring `app.MapScalarApiReference()` after `app.MapOpenApi()` in `Program.cs`.

#### Scenario: Scalar UI accessible after fix

- GIVEN the application is running with the Scalar line added
- WHEN a browser requests `/scalar/v1`
- THEN the Scalar API reference UI is rendered

---

### Requirement: REQ-FIX-2 — vue-i18n Linked-Message @ Bug

The four email placeholder values containing `@` in `en.json` and `es.json` MUST be escaped as `{'@'}` to prevent vue-i18n runtime linked-message errors.

Affected locations:
| File | Line | Key |
|---|---|---|
| `en.json` | 13 | `auth.login.emailPlaceholder` |
| `en.json` | 23 | `auth.register.emailPlaceholder` |
| `es.json` | 13 | `auth.login.emailPlaceholder` |
| `es.json` | 23 | `auth.register.emailPlaceholder` |

#### Scenario: No linked-message error at login

- GIVEN the `@` characters are escaped in all four locations
- WHEN the login page renders
- THEN no vue-i18n runtime warning or error appears in the browser console

---

### Requirement: REQ-FIX-3 — Login/Register Visual Alignment

`LoginView` and `RegisterView` MUST render correctly under `PublicLayout` with daisyUI v5 form-control semantics. The "Language" label in `RegisterView` MUST use an i18n key (`auth.register.languageLabel`) instead of hardcoded text.

#### Scenario: Language label uses i18n key

- GIVEN the fix is applied and locale is set to "es"
- WHEN the register page renders
- THEN the language selector label shows the Spanish translation, not the hardcoded "Language" string
