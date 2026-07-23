# Tasks: Language — Full i18n Wiring

## Review Workload Forecast

| Field | Value |
|-------|-------|
| Estimated changed lines | 380–480 |
| 400-line budget risk | Medium |
| Chained PRs recommended | Yes |
| Suggested split | PR 1 (Slice 1 — frontend only) → PR 2 (Slice 2 — backend + store write-back) |
| Delivery strategy | ask-on-risk |
| Chain strategy | stacked-to-main |

Decision needed before apply: Yes
Chained PRs recommended: Yes
Chain strategy: stacked-to-main
400-line budget risk: Medium

### Suggested Work Units

| Unit | Goal | Likely PR | Notes |
|------|------|-----------|-------|
| 1 | Frontend-only: switcher mount, hardcoded string fixes, locale seeding, aria-label | PR 1 | Base: `feat/language`; self-contained, zero backend changes |
| 2 | Backend vertical slice + store write-back | PR 2 | Base: PR 1 branch (stacked); depends on PR 1 locale.store shape |

---

## Phase 1: Foundation — i18n Keys and Normalization Helper

- [x] 1.1 Add keys to `Project/frontend/src/i18n/locales/en.json`: `common.noNotifications`, `common.switchLanguage`, `enums.periodStatus.{open,closed,locked}`, `enums.role.{admin,operator,readOnly}`.
- [x] 1.2 Add matching Spanish keys to `Project/frontend/src/i18n/locales/es.json` with correct translations.
- [x] 1.3 Add normalization helper in `Project/frontend/src/utils/enum-key.ts` (or inline as a local map) that converts kebab-case role values from the server (e.g. `"read-only"`) to the camelCase i18n key suffix (e.g. `"readOnly"`). Required because `enums.role.readOnly` !== `enums.role.read-only`. Export as `roleKeyMap` or a `toRoleKey(value: string): string` function.

## Phase 2: Slice 1 — Frontend Wiring (PR 1)

- [x] 2.1 `LanguageSwitcher.vue` — add `aria-label` bound to `$t('common.switchLanguage')` on the wrapper `<div role="group">`.
- [x] 2.2 `AppLayout.vue` — import `LanguageSwitcher`, insert as `<li>` in user dropdown between "Change password" and "Logout". Replace hardcoded `"No notifications"` (line 201) with `$t('common.noNotifications')`.
- [x] 2.3 `AppLayout.vue` — fix `activeRoleBadge` computed (lines 48–52): use `$t('enums.role.' + toRoleKey(role))` via the normalization helper from 1.3 (server sends kebab-case; i18n keys are camelCase).
- [x] 2.4 `PublicLayout.vue` — add `<div class="absolute top-4 right-4"><LanguageSwitcher /></div>` above the card slot.
- [x] 2.5 `InviteUserModal.vue` — replace hardcoded `<option>` labels (Admin / Operator / Read Only) with `$t('enums.role.admin')`, `$t('enums.role.operator')`, `$t('enums.role.readOnly')`. Note: option values are static strings sent to the API, only the visible label is translated.
- [x] 2.6 `CycleDetailView.vue` — replace hardcoded ternary `'Closed' : 'Open'` (line 114) with `$t('enums.periodStatus.' + status.toLowerCase())`. Replace hardcoded status `<option>` labels (lines 213–215) with `$t('enums.periodStatus.*')`.
- [x] 2.7 `BudgetSelectionView.vue` — replace `{{ m.role }}` (line 64) with `$t('enums.role.' + toRoleKey(m.role))` using the normalization helper; remove `capitalize` class.
- [x] 2.8 `auth.store.ts` — inside `fetchMe()`, after `user.value = data`, add: `if (!localStorage.getItem('locale') && data.preferredLocale) { localeStore.setLocale(data.preferredLocale as SupportedLocale, true) }`. The `skipSync = true` flag prevents write-back to server during seeding.

## Phase 3: Slice 2 — Backend Vertical Slice + Store Write-Back (PR 2)

- [x] 3.1 `User.cs` — add `UpdateLocale(string locale)` method: sets `PreferredLocale` and `UpdatedAt = DateTimeOffset.UtcNow`.
- [x] 3.2 Create `UpdateLocaleCommand.cs`: `public sealed record UpdateLocaleCommand(string Locale) : IRequest<Result<Unit>>;`
- [x] 3.3 Create `UpdateLocaleValidator.cs`: FluentValidation rule — `Locale` not empty, error code `FIELD_REQUIRED`.
- [x] 3.4 Create `UpdateLocaleHandler.cs`: resolve user via `ICurrentUserService`, load via EF, validate `Locale` against `SupportedCultures` from `IConfiguration`, call `user.UpdateLocale()`, save. Return 422 with `AUTH_LOCALE_UNSUPPORTED` on invalid locale.
- [x] 3.5 Create `UpdateLocaleEndpoint.cs`: `MapPatch("/api/auth/me/locale")`, `.RequireAuthorization()`, returns 204 on success / 422 on validation failure. Auto-discovered by `MapAllSliceEndpoints`.
- [x] 3.6 `locale.store.ts` — update `setLocale(lang: SupportedLocale, skipSync = false)`: after localStorage write, if `!skipSync && authStore.isAuthenticated`, fire-and-forget `PATCH /api/auth/me/locale { locale: lang }`.

## Phase 4: Testing (TDD — RED before GREEN for each)

- [x] 4.1 **RED** `UpdateLocaleValidator` unit test: assert empty `Locale` produces `FIELD_REQUIRED` error. File: `Project/test/MyBudget.Features.Tests/Features/Auth/UpdateLocaleValidatorTests.cs`.
- [x] 4.2 **GREEN** Make validator pass (task 3.3).
- [x] 4.3 **RED** `UpdateLocaleHandler` integration tests (3 cases): (a) valid locale → 204 + DB updated; (b) unsupported locale → 422 `AUTH_LOCALE_UNSUPPORTED`; (c) unauthenticated → 401. File: `Project/test/MyBudget.Integration.Tests/Features/Auth/UpdateLocaleHandlerTests.cs`.
- [x] 4.4 **GREEN** Make handler pass (tasks 3.4–3.5).
- [x] 4.5 **RED** `locale.store.ts` unit tests: (a) PATCH called when `isAuthenticated=true` and `skipSync=false`; (b) PATCH not called when `skipSync=true`; (c) PATCH not called when unauthenticated. File: `Project/frontend/src/stores/__tests__/locale.store.spec.ts`.
- [x] 4.6 **GREEN** Make locale.store pass (task 3.6).
- [x] 4.7 **RED** `auth.store.ts` unit test: `fetchMe` seeds locale only when `localStorage('locale')` is absent; does NOT override when key is present. File: `Project/frontend/src/stores/__tests__/auth.store.spec.ts`.
- [x] 4.8 **GREEN** Make auth.store seeding pass (task 2.8).
- [x] 4.9 **RED** `LanguageSwitcher.vue` component test: assert `aria-label` attribute is non-empty. File: `Project/frontend/src/components/__tests__/LanguageSwitcher.spec.ts`.
- [x] 4.10 **GREEN** Make aria-label pass (task 2.1).
- [x] 4.11 **RED** `InviteUserModal.vue` component test: mount with `locale=es`, assert role `<option>` labels render Spanish translations (e.g. "Solo lectura" not "Read Only"). File: `Project/frontend/src/components/__tests__/InviteUserModal.spec.ts`.
- [x] 4.12 **GREEN** Make InviteUserModal pass (task 2.5 + i18n keys from Phase 1).

## Phase 5: Cleanup

- [x] 5.1 Verify no remaining hardcoded English strings in `AppLayout.vue`, `PublicLayout.vue`, `InviteUserModal.vue`, `CycleDetailView.vue`, `BudgetSelectionView.vue` via `rg -n '"[A-Z][a-z]'` scoped to those files.
- [x] 5.2 Confirm `en.json` and `es.json` have identical key sets (no missing translations).
- [ ] 5.3 Confirm `LanguageSwitcher` is visible in both AppLayout user dropdown and on all PublicLayout pages in browser smoke test.
