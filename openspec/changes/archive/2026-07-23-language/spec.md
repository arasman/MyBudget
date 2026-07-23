# Spec: language — Full i18n Wiring

## Capabilities

| Domain | Type | Spec file |
|--------|------|-----------|
| `locale-sync` | New | `specs/locale-sync/spec.md` |
| `app-layout` | Delta | `specs/app-layout/spec.md` |
| `auth` | Delta | `specs/auth/spec.md` |

## Summary of Requirements

### New — locale-sync

| ID | Requirement |
|----|-------------|
| LSYNC-1 | Locale seeded from server on first login only when localStorage has no `locale` key |
| LSYNC-2 | Authenticated locale switch triggers PATCH; unauthenticated switch writes localStorage only |
| LSYNC-3 | `PATCH /api/auth/me/locale` — authenticated, validates against SupportedCultures, returns 204 |
| LSYNC-4 | Shared enum i18n keys (`enums.periodStatus.*`, `enums.role.*`, `common.noNotifications`) in both locales |
| LSYNC-5 | `LanguageSwitcher` has non-empty `aria-label` |

### Modified — app-layout

| ID | Requirement | Change |
|----|-------------|--------|
| LAYOUT-2 | Public Layout Shell | Adds header with LanguageSwitcher; adds `/forgot-password`, `/reset-password` to covered routes |
| NAV-3 | Notification Bell | Empty-state message uses `common.noNotifications` i18n key instead of hardcoded string |
| NAV-4 | User Dropdown | Adds LanguageSwitcher inside dropdown |

### Added — auth

| ID | Requirement |
|----|-------------|
| AUTH-LOCALE-1 | PATCH locale endpoint wired to auth domain |
| AUTH-LOCALE-2 | `fetchMe` response includes `preferredLocale` field |
