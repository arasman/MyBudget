# Design: Language — Full i18n Wiring

## Technical Approach

Wire existing i18n infrastructure end-to-end: mount LanguageSwitcher in both layouts, sync locale between server and client via a new PATCH endpoint, replace hardcoded English strings with shared i18n keys. Follows existing vertical-slice backend pattern (Command + Handler + Endpoint + Validator) and Pinia store conventions.

## Architecture Decisions

| Decision | Choice | Rejected | Rationale |
|----------|--------|----------|-----------|
| Write-back trigger | Explicit action wrapper in `locale.store.ts` that checks `authStore.isAuthenticated` | Pinia `$subscribe` / Vue `watch` | Action wrapper is explicit, testable, avoids circular dependency risk from watchers importing auth store reactively |
| Locale seeding location | Inside `auth.store.fetchMe()`, after setting `user.value` | Separate composable or router guard | Keeps logic co-located with the data source; `fetchMe` is the single place user data arrives. Guard would require extra wiring |
| LanguageSwitcher aria-label | Localized via i18n key `common.switchLanguage` | Static English string | Screen reader experience should match active locale |
| Backend validation source | Read `SupportedCultures` from `IConfiguration` at handler level | Hardcode `["en","es"]` in validator | Single source of truth in `appsettings.json`; adding a locale later needs no code change |
| PATCH response | `204 No Content` | `200 OK` with body | No meaningful data to return; follows REST convention for updates |

## Data Flow

```
LanguageSwitcher
      │
      ▼
localeStore.setLocale(lang)
      │
      ├─── Updates vue-i18n global locale
      ├─── Writes localStorage('locale')
      ├─── Sets Axios Accept-Language header
      └─── IF authStore.isAuthenticated
              │
              ▼
         PATCH /api/auth/me/locale { locale: "es" }
              │
              ▼
    UpdateLocaleHandler → User.UpdateLocale(locale)
              │
              ▼
         204 No Content


Login flow (fetchMe):
      │
      ▼
auth.store.fetchMe() → GET /api/auth/me
      │
      ▼
  user.value = data
      │
      ▼
  IF !localStorage.getItem('locale')
      │
      ▼
  localeStore.setLocale(data.preferredLocale)
  (no PATCH — seeding only)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `Project/frontend/src/stores/locale.store.ts` | Modify | Import `useAuthStore`; in `setLocale()`, after localStorage write, call `PATCH /api/auth/me/locale` when `authStore.isAuthenticated`. Fire-and-forget (no await needed for UI). |
| `Project/frontend/src/stores/auth.store.ts` | Modify | After `user.value = data` in `fetchMe()`, check `!localStorage.getItem('locale')` — if true, import and call `localeStore.setLocale(data.preferredLocale)` without triggering PATCH (guard: user not yet authenticated at that point, or pass a `skipSync` flag). |
| `Project/frontend/src/layouts/AppLayout.vue` | Modify | Import `LanguageSwitcher`; add it as a `<li>` in the user dropdown (between "Change password" and "Logout"). Replace hardcoded `"No notifications"` (line 201) with `$t('common.noNotifications')`. Replace `activeRoleBadge` computed (line 48-52) to use `$t('enums.role.' + role)`. |
| `Project/frontend/src/layouts/PublicLayout.vue` | Modify | Add a fixed top-right absolute-positioned `LanguageSwitcher` above the card. Wrap in a `<div class="absolute top-4 right-4">`. |
| `Project/frontend/src/components/LanguageSwitcher.vue` | Modify | Add `aria-label` bound to `$t('common.switchLanguage')` on the wrapper `<div>` with `role="group"`. |
| `Project/frontend/src/components/budget/InviteUserModal.vue` | Modify | Replace hardcoded `<option>` labels (Admin/Operator/Read Only) with `$t('enums.role.admin')`, `$t('enums.role.operator')`, `$t('enums.role.readOnly')`. |
| `Project/frontend/src/features/budget-structure/views/CycleDetailView.vue` | Modify | Replace hardcoded status text (line 114: `'Closed' : 'Open'`) with `$t('enums.periodStatus.' + status.toLowerCase())`. Replace hardcoded `<option>` labels in status dialog (lines 213-215) with `$t('enums.periodStatus.*')`. |
| `Project/frontend/src/features/budget-structure/views/BudgetSelectionView.vue` | Modify | Replace `{{ m.role }}` (line 64) with `$t('enums.role.' + m.role)`, remove `capitalize` class. |
| `Project/frontend/src/i18n/locales/en.json` | Modify | Add keys: `common.noNotifications`, `common.switchLanguage`, `enums.periodStatus.{open,closed,locked}`, `enums.role.{admin,operator,readOnly}`. |
| `Project/frontend/src/i18n/locales/es.json` | Modify | Add same keys with Spanish translations. |
| `Project/src/MyBudget.Features/SharedKernel/Entities/User.cs` | Modify | Add `UpdateLocale(string locale)` method (sets `PreferredLocale`, updates `UpdatedAt`). |
| `Project/src/MyBudget.Features/Features/Auth/UpdateLocale/UpdateLocaleCommand.cs` | Create | `record UpdateLocaleCommand(string Locale) : IRequest<Result<Unit>>` |
| `Project/src/MyBudget.Features/Features/Auth/UpdateLocale/UpdateLocaleHandler.cs` | Create | Resolves user via `ICurrentUserService`, loads via EF, calls `User.UpdateLocale()`, validates locale against `SupportedCultures` from `IConfiguration`, saves. |
| `Project/src/MyBudget.Features/Features/Auth/UpdateLocale/UpdateLocaleEndpoint.cs` | Create | `MapPatch("/api/auth/me/locale", Handle)`, `.RequireAuthorization()`, returns 204 or 422. Auto-discovered by `MapAllSliceEndpoints`. |
| `Project/src/MyBudget.Features/Features/Auth/UpdateLocale/UpdateLocaleValidator.cs` | Create | FluentValidation: `Locale` not empty, with error code `FIELD_REQUIRED`. |

## Interfaces / Contracts

```csharp
// UpdateLocaleCommand.cs
public sealed record UpdateLocaleCommand(string Locale) : IRequest<Result<Unit>>;

// UpdateLocaleEndpoint.cs — request DTO
private sealed record UpdateLocaleRequest(string Locale);
// Route: PATCH /api/auth/me/locale
// Success: 204 No Content
// Failure: 422 { detail: "AUTH_LOCALE_UNSUPPORTED" }
// Unauth: 401 (via RequireAuthorization)

// User.cs addition
public void UpdateLocale(string locale)
{
    PreferredLocale = locale;
    UpdatedAt = DateTimeOffset.UtcNow;
}
```

```typescript
// locale.store.ts — setLocale signature unchanged, internal logic added
function setLocale(lang: SupportedLocale, skipSync = false): void

// auth.store.ts — fetchMe addition (pseudo)
// after: user.value = data
// if (!localStorage.getItem('locale') && data.preferredLocale) {
//   const localeStore = useLocaleStore()
//   localeStore.setLocale(data.preferredLocale as SupportedLocale, true)
// }
```

```json
// New i18n keys (en.json)
{
  "common": {
    "noNotifications": "No notifications",
    "switchLanguage": "Switch language"
  },
  "enums": {
    "periodStatus": { "open": "Open", "closed": "Closed", "locked": "Locked" },
    "role": { "admin": "Admin", "operator": "Operator", "readOnly": "Read Only" }
  }
}

// es.json
{
  "common": {
    "noNotifications": "Sin notificaciones",
    "switchLanguage": "Cambiar idioma"
  },
  "enums": {
    "periodStatus": { "open": "Abierto", "closed": "Cerrado", "locked": "Bloqueado" },
    "role": { "admin": "Administrador", "operator": "Operador", "readOnly": "Solo lectura" }
  }
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `UpdateLocaleValidator` — empty locale rejected | FluentValidation test (existing pattern in `Features.Tests`) |
| Unit | `locale.store.ts` — setLocale calls PATCH only when authenticated, skips when `skipSync=true` | Vitest with mocked `authStore` and `http` |
| Unit | `auth.store.ts` — fetchMe seeds locale only when localStorage absent | Vitest with mocked localStorage |
| Integration | `UpdateLocaleHandler` — valid/invalid locale, unauthorized | Integration test (existing pattern in `Integration.Tests/Features/Auth/`) |
| Component | LanguageSwitcher has `aria-label` | Vitest + Vue Test Utils, assert attribute |
| Component | InviteUserModal role options use i18n keys | Vitest + Vue Test Utils, locale=es, assert Spanish labels |

## Migration / Rollout

No migration required. `User.PreferredLocale` column already exists with default `"en"`. The new `UpdateLocale` method only mutates an existing column.

## Open Questions

None. All technical decisions are resolved.
