# ephemeral-toast Specification

## Purpose

Defines the ephemeral toast overlay system — a transient, auto-dismissing feedback layer separate
from the persistent notification bell. Used to confirm create/delete/restore actions across all
entities.

---

## Requirements

### Requirement: REQ-TOAST-1 — Toast Store

The system MUST provide a `useToastStore` Pinia store (or equivalent module-level reactive
singleton) that manages a list of active toast messages. Each toast MUST have: `id` (auto-generated),
`message` (string), `type` (`success` | `error` | `info`), and `autoDismiss` (ms, default 3000).

#### Scenario: Push and auto-dismiss

- GIVEN the toast store is initialized
- WHEN `push({ message: "Done", type: "success" })` is called
- THEN a toast entry appears in the list
- AND it is removed automatically after 3000 ms

#### Scenario: Manual close removes toast

- GIVEN a toast is visible
- WHEN the user clicks its × button
- THEN the toast is removed immediately without waiting for the timer

#### Scenario: Multiple toasts stack

- GIVEN two pushes with no dismissal between them
- WHEN the component renders
- THEN both toasts are visible simultaneously (stacked)

---

### Requirement: REQ-TOAST-2 — AppToast Component

`AppToast.vue` MUST be mounted once in `AppLayout.vue`. It MUST render all active toasts as a
stack using DaisyUI `toast` + `alert` classes. Each toast MUST include a visible × close button.
The component MUST be positioned at a z-index above modals so toasts remain visible when a modal
is open.

#### Scenario: Renders at correct z-index

- GIVEN a modal is open in the application
- WHEN a toast is pushed
- THEN the toast overlay appears above the modal without being clipped

#### Scenario: Close button on each toast

- GIVEN two stacked toasts are visible
- WHEN the user clicks × on the first toast
- THEN only that toast is removed; the second toast remains

---

### Requirement: REQ-TOAST-3 — Bell Exclusion

Toasts pushed via `useToastStore` MUST NOT be written to `useNotificationStore`. The notification
bell dropdown MUST only contain persistent notifications. Auto-dismiss toasts MUST NOT accumulate
in the bell inbox.

#### Scenario: Toast does not appear in bell

- GIVEN a delete success toast is pushed via useToastStore
- WHEN the user opens the notification bell dropdown
- THEN no entry corresponding to that toast appears in the bell list

#### Scenario: Existing bell notifications unaffected

- GIVEN a persistent notification exists in the bell
- WHEN a toast is pushed and auto-dismissed
- THEN the bell notification count is unchanged

---

### Requirement: REQ-TOAST-I18N-1 — Toast i18n Keys

The following keys MUST be present in both `en.json` and `es.json` under their entity namespaces.

| Namespace | Key | Purpose |
|---|---|---|
| `budgetStructure.cycles` | `createSuccess` | Cycle created |
| `budgetStructure.cycles` | `deleteSuccess` | Cycle deleted |
| `budgetStructure.cycles` | `restoreSuccess` | Cycle restored |
| `budgetStructure.cycles` | `showDeleted` | Toggle label |
| `budgetStructure.periods` | `createSuccess` | Period created |
| `budgetStructure.periods` | `deleteSuccess` | Period deleted |
| `budgetStructure.periods` | `restoreSuccess` | Period restored |
| `budgetStructure.periods` | `showDeleted` | Toggle label |
| `budgetStructure.categoryGroups` | `createSuccess` | Group created |
| `budgetStructure.categoryGroups` | `deleteSuccess` | Group deleted |
| `budgetStructure.categoryGroups` | `restoreSuccess` | Group restored |
| `budgetStructure.categoryGroups` | `showDeleted` | Toggle label |
| `budgetStructure.categories` | `createSuccess` | Category created |
| `budgetStructure.categories` | `deleteSuccess` | Category deleted |
| `budgetStructure.categories` | `restoreSuccess` | Category restored |
| `budgetStructure.categories` | `showDeleted` | Toggle label |
| `budgetStructure.budgetLines` | `createSuccess` | Line created |
| `budgetStructure.budgetLines` | `deleteSuccess` | Line deleted |
| `budgetStructure.budgetLines` | `restoreSuccess` | Line restored |
| `budgetStructure.budgetLines` | `showDeleted` | Toggle label |
| `budgetExecution.record` | `deleteSuccess` | Record deleted |
| `budgetExecution.record` | `restoreSuccess` | Record restored |

#### Scenario: All keys present in both locales

- GIVEN the application builds with locale files loaded
- WHEN any toast message references the keys above
- THEN no i18n missing-key warning is emitted in either EN or ES locale
