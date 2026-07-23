# Proposal: Language — Full i18n Wiring

## Intent

The i18n foundation (vue-i18n v9, en/es JSON files, locale store, backend `User.PreferredLocale`) is complete but disconnected. The `LanguageSwitcher` component exists but is not mounted anywhere. Server-side locale preference is never applied after login. Hardcoded English strings remain in four components. Users cannot change their language preference post-registration. This change wires everything together so locale works end-to-end.

## Scope

### In Scope
- Mount `LanguageSwitcher` in `AppLayout` user dropdown and `PublicLayout` header (all public pages)
- Apply server `preferredLocale` to locale store on first login (localStorage absent)
- Add `PATCH /api/auth/me/locale` endpoint (any authenticated user, own locale only)
- Wire locale store to call PATCH when authenticated user switches language
- Replace hardcoded strings: "No notifications", period statuses, role labels
- Add shared enum i18n keys (`enums.periodStatus.*`, `enums.role.*`)
- Add `aria-label` to `LanguageSwitcher`

### Out of Scope
- Adding new locales beyond en/es
- Backend error message localization (already uses error codes)
- RTL layout support
- Locale-aware date/number formatting

## Capabilities

### New Capabilities
- `locale-sync`: Server-client locale synchronization (PATCH endpoint, fetchMe seeding, write-back on switch)

### Modified Capabilities
- `app-layout`: Mount LanguageSwitcher in user dropdown, fix "No notifications" hardcoded string
- `auth`: Add PATCH `/api/auth/me/locale` endpoint, seed locale store from `fetchMe`

## Approach

**Two slices:**

**Slice 1 (frontend only):** Mount switcher in AppLayout user dropdown + PublicLayout top-right header bar. Seed locale store from `fetchMe` response only when `localStorage('locale')` is absent (server wins on first login, explicit choice preserved). Replace four hardcoded string clusters with shared enum keys. Add accessibility `aria-label`.

**Slice 2 (backend + frontend):** Add `PATCH /api/auth/me/locale` — validates locale against `SupportedCultures`, updates `User.PreferredLocale`. Wire `locale.store.setLocale()` to call PATCH when `authStore.isAuthenticated`. No role check beyond authentication.

**Locale conflict rule:** Public pages use localStorage (or browser default). After login, `fetchMe` applies `User.PreferredLocale` only when localStorage has no `locale` key. Authenticated language switch writes back to server via PATCH.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `frontend/src/components/LanguageSwitcher.vue` | Modified | Add aria-label |
| `frontend/src/layouts/AppLayout.vue` | Modified | Mount switcher, fix hardcoded "No notifications" |
| `frontend/src/layouts/PublicLayout.vue` | Modified | Add header bar with switcher |
| `frontend/src/stores/locale.store.ts` | Modified | Add authenticated write-back logic |
| `frontend/src/stores/auth.store.ts` | Modified | Seed locale from fetchMe on first login |
| `frontend/src/views/.../PeriodForm.vue` | Modified | Replace hardcoded status labels |
| `frontend/src/views/.../CycleDetailView.vue` | Modified | Replace hardcoded status labels |
| `frontend/src/views/.../InviteUserModal.vue` | Modified | Replace hardcoded role labels |
| `frontend/src/i18n/locales/en.json` | Modified | Add enum + notification keys |
| `frontend/src/i18n/locales/es.json` | Modified | Add enum + notification keys |
| `backend Features/Auth/` | New | PATCH locale endpoint (handler + validator) |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Locale flicker on login (server overrides mid-session choice) | Low | Only apply server locale when localStorage key absent |
| PATCH endpoint missing validation allows unsupported locale | Low | Validate against `SupportedCultures` list server-side |
| Enum key mismatch between JSON files and component usage | Low | Spec will define exact key names; parity check in tests |

## Rollback Plan

Revert the feature branch. The LanguageSwitcher stays unmounted (current state). PATCH endpoint is additive — its migration (if any) is a no-op column already exists. No data migration risk.

## Dependencies

- `User.PreferredLocale` column already exists in DB
- `UseRequestLocalization()` already configured in `Program.cs`
- `LanguageSwitcher.vue` component already built

## Success Criteria

- [ ] LanguageSwitcher visible on all public pages and in AppLayout user dropdown
- [ ] First login on new device applies server-stored locale preference
- [ ] Switching language while authenticated persists to server via PATCH
- [ ] Zero hardcoded English strings in AppLayout, PeriodForm, CycleDetailView, InviteUserModal
- [ ] LanguageSwitcher has `aria-label` for screen readers
- [ ] All enum labels (period status, roles) use shared i18n keys
