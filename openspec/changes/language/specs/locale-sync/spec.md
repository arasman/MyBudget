# locale-sync Specification

## Purpose

Defines end-to-end locale synchronization: server-authoritative locale seeded on first login, authenticated write-back via PATCH, and shared i18n enum keys used by all components.

---

## Requirements

### Requirement: LSYNC-1 — Locale Seeding on First Login

The system MUST apply `User.PreferredLocale` from the `fetchMe` response to the locale store on successful login, but ONLY when `localStorage` has no `locale` key. If `localStorage` already contains a `locale` key, the stored value MUST be preserved without overwriting.

#### Scenario: First login on new device applies server locale

- GIVEN `localStorage` has no `locale` key
- AND the user's `preferredLocale` on the server is `"es"`
- WHEN `fetchMe` resolves after a successful login
- THEN `localeStore.setLocale("es")` is called without a PATCH request
- AND the active vue-i18n locale switches to `"es"`

#### Scenario: Manual pre-login switch is preserved after login

- GIVEN the user switched language to `"es"` before logging in (localStorage key `locale = "es"`)
- WHEN `fetchMe` resolves after login
- THEN the locale store keeps `"es"` unchanged
- AND no PATCH request is made

---

### Requirement: LSYNC-2 — Authenticated Locale Write-Back

When an authenticated user switches locale, the system MUST send `PATCH /api/auth/me/locale` with the new locale value. The write-back MUST NOT occur for unauthenticated users; it MUST only write localStorage.

#### Scenario: Authenticated user switches locale

- GIVEN the user is authenticated
- WHEN `localeStore.setLocale("en")` is called
- THEN a `PATCH /api/auth/me/locale` request is sent with body `{ locale: "en" }`
- AND `localStorage.locale` is updated to `"en"`
- AND the active i18n locale switches to `"en"`

#### Scenario: Unauthenticated user switches locale

- GIVEN the user is not authenticated (public page)
- WHEN `localeStore.setLocale("es")` is called
- THEN no PATCH request is sent
- AND `localStorage.locale` is updated to `"es"`
- AND the active i18n locale switches to `"es"`

---

### Requirement: LSYNC-3 — PATCH /api/auth/me/locale Endpoint

The system MUST expose `PATCH /api/auth/me/locale`. The request MUST be authenticated (valid JWT). The endpoint MUST validate the submitted locale against `SupportedCultures` (`"en"`, `"es"`). On success it MUST update `User.PreferredLocale` for the caller only and return `204 No Content`.

| Field | Rule |
|-------|------|
| `locale` | REQUIRED. Must be `"en"` or `"es"`. |

#### Scenario: Valid locale update

- GIVEN an authenticated user
- WHEN `PATCH /api/auth/me/locale` is called with `{ locale: "es" }`
- THEN `User.PreferredLocale` is updated to `"es"` for that user
- AND the response is `204 No Content`

#### Scenario: Unsupported locale rejected

- GIVEN an authenticated user
- WHEN `PATCH /api/auth/me/locale` is called with `{ locale: "fr" }`
- THEN the response is `422 Unprocessable Entity` with error code `AUTH_LOCALE_UNSUPPORTED`

#### Scenario: Unauthenticated request rejected

- GIVEN no valid JWT in the request
- WHEN `PATCH /api/auth/me/locale` is called
- THEN the response is `401 Unauthorized`

---

### Requirement: LSYNC-4 — Shared Enum i18n Keys

The system MUST define shared i18n keys for period statuses and role labels used across components. Both `en.json` and `es.json` MUST contain these keys. Components MUST NOT contain hardcoded English display strings for these values.

Required key structure:

| Key | EN value | ES value |
|-----|----------|----------|
| `enums.periodStatus.open` | `"Open"` | `"Abierto"` |
| `enums.periodStatus.closed` | `"Closed"` | `"Cerrado"` |
| `enums.periodStatus.locked` | `"Locked"` | `"Bloqueado"` |
| `enums.role.admin` | `"Admin"` | `"Administrador"` |
| `enums.role.operator` | `"Operator"` | `"Operador"` |
| `enums.role.readOnly` | `"Read Only"` | `"Solo lectura"` |
| `common.noNotifications` | `"No notifications"` | `"Sin notificaciones"` |

#### Scenario: Period status rendered via i18n key

- GIVEN the locale is `"es"`
- WHEN a component renders a period with `status = "open"`
- THEN the displayed text is `"Abierto"` (from `enums.periodStatus.open`)
- AND no hardcoded English string is present in the template

#### Scenario: Role label rendered via i18n key

- GIVEN the locale is `"es"`
- WHEN the user dropdown renders the active budget role `"admin"`
- THEN the displayed text is `"Administrador"` (from `enums.role.admin`)

#### Scenario: No-notifications message via i18n key

- GIVEN the notification panel is open and `notificationStore.items` is empty
- WHEN the locale is `"es"`
- THEN the displayed text is `"Sin notificaciones"` (from `common.noNotifications`)

---

### Requirement: LSYNC-5 — LanguageSwitcher Accessibility

The `LanguageSwitcher` component MUST include an `aria-label` attribute to identify the control to screen readers.

#### Scenario: aria-label present on switcher

- GIVEN the LanguageSwitcher is rendered
- WHEN the DOM is inspected
- THEN the control element has a non-empty `aria-label` attribute
