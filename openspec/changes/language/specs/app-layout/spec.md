# Delta for app-layout

## MODIFIED Requirements

### Requirement: LAYOUT-2 — Public Layout Shell

The system MUST render `PublicLayout.vue` as the parent component for `/login`, `/register`, `/forgot-password`, `/reset-password`, and `/invitations/accept`. `PublicLayout` MUST render a centered card container with no authenticated navbar. `PublicLayout` MUST include a header bar that renders `LanguageSwitcher` so unauthenticated users can change locale from any public page.

(Previously: PublicLayout had no header — it was a centered card with no chrome at all. `/forgot-password` and `/reset-password` were not explicitly listed.)

#### Scenario: Login page renders inside PublicLayout

- GIVEN the user navigates to `/login`
- WHEN the route resolves
- THEN a centered card is rendered without an authenticated navbar

#### Scenario: LanguageSwitcher visible on all public pages

- GIVEN the user is on any public route (`/login`, `/register`, `/forgot-password`, `/reset-password`, `/invitations/accept`)
- WHEN the page renders
- THEN `LanguageSwitcher` is visible in the PublicLayout header

---

### Requirement: NAV-3 — Notification Bell

The navbar MUST display a bell icon button. When `notificationStore.unreadCount > 0`, a badge with the count MUST be visible on the icon. Clicking the bell MUST toggle a dropdown panel listing notifications from `notificationStore.items`. The empty-state message MUST be rendered using the i18n key `common.noNotifications`. The notification system is infrastructure only — no backend source is wired in this change.

(Previously: The empty-state message was a hardcoded English string "No notifications".)

#### Scenario: Badge appears when unread count is nonzero

- GIVEN `notificationStore.unreadCount = 3`
- WHEN the navbar renders
- THEN a badge displaying "3" is visible on the bell icon

#### Scenario: Empty notification panel uses i18n key

- GIVEN `notificationStore.items` is empty
- AND the locale is `"es"`
- WHEN the bell is clicked
- THEN the dropdown panel shows `"Sin notificaciones"` (resolved from `common.noNotifications`)

---

### Requirement: NAV-4 — User Dropdown

The navbar MUST display a user dropdown triggered by the user's initials (derived from `firstName` + `lastName`). The dropdown MUST show the user's role badge for the active budget, a `LanguageSwitcher` control, and a logout action. Clicking logout MUST call `authStore.logout()` and redirect to `/login`.

(Previously: The user dropdown contained no LanguageSwitcher.)

#### Scenario: Initials derived correctly

- GIVEN a user with `firstName = "Ana"` and `lastName = "López"`
- WHEN the navbar renders
- THEN the dropdown trigger displays "AL"

#### Scenario: LanguageSwitcher visible in user dropdown

- GIVEN the user is authenticated and opens the user dropdown
- WHEN the dropdown is rendered
- THEN `LanguageSwitcher` is visible inside the dropdown

#### Scenario: Logout redirects to login

- GIVEN the user clicks logout in the user dropdown
- WHEN `authStore.logout()` resolves
- THEN the router navigates to `/login`
