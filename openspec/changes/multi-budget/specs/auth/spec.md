# Delta for Auth

## MODIFIED Requirements

### Requirement: ME-1 — Authenticated Profile Retrieval

The system MUST return the current user's profile including a `memberships` array. Each membership
entry MUST include an `isDeleted` flag indicating whether the associated Budget is soft-deleted.
Soft-deleted memberships MUST be included in the response — they are NOT filtered out.
This is a read-only Dapper query — no EF Core. Requires valid JWT access token.

(Previously: ME-1 did not include `isDeleted` on membership entries; no memberships array was
documented in the response.)

**Response shape:** `200 OK` with:
```
{
  id, email, firstName, lastName, preferredLocale, lastLoginAt, createdAt,
  memberships: [
    { budgetId, budgetName, role, isDeleted }
  ]
}
```

#### Scenario: Happy path — active memberships

- GIVEN an authenticated user with two active budget memberships
- WHEN `GET /api/auth/me` is called with `Authorization: Bearer {accessToken}`
- THEN `200 OK` is returned with user profile fields
- AND `memberships` array contains two entries each with `isDeleted: false`

#### Scenario: Soft-deleted membership included

- GIVEN an authenticated user with one active and one soft-deleted budget membership
- WHEN `GET /api/auth/me` is called
- THEN `200 OK` is returned
- AND `memberships` contains two entries: one with `isDeleted: false`, one with `isDeleted: true`

#### Scenario: All memberships deleted

- GIVEN an authenticated user whose only budget has `IsDeleted = true`
- WHEN `GET /api/auth/me` is called
- THEN `200 OK` is returned
- AND `memberships` contains one entry with `isDeleted: true`

#### Scenario: Expired access token

- GIVEN an expired JWT
- WHEN `GET /api/auth/me` is called
- THEN `401 Unauthorized` is returned (standard Bearer challenge)

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Missing / expired token | 401 | (standard Bearer challenge) |
