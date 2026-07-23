# Delta for auth

## ADDED Requirements

### Requirement: AUTH-LOCALE-1 — PATCH Locale Endpoint

The system MUST expose `PATCH /api/auth/me/locale` for any authenticated user to update their own `PreferredLocale`. This requirement is fully specified in `openspec/changes/language/specs/locale-sync/spec.md` as LSYNC-3.

#### Scenario: Authenticated user updates own locale

- GIVEN a valid JWT is present in the request
- WHEN `PATCH /api/auth/me/locale` is called with `{ locale: "es" }`
- THEN `User.PreferredLocale` is updated for the token owner
- AND the response is `204 No Content`

---

### Requirement: AUTH-LOCALE-2 — Locale Seeding from fetchMe

After a successful login, the system MUST read `preferredLocale` from the `GET /api/auth/me` response and conditionally apply it to the frontend locale store. This requirement is fully specified in `openspec/changes/language/specs/locale-sync/spec.md` as LSYNC-1.

#### Scenario: fetchMe response includes preferredLocale

- GIVEN the user is authenticated
- WHEN `GET /api/auth/me` is called
- THEN the response body includes a `preferredLocale` field with a value of `"en"` or `"es"`
