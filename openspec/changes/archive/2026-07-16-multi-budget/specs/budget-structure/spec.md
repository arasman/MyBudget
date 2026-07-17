# Delta for Budget Structure

## MODIFIED Requirements

### Requirement: AUTHZ-1 — Role Resolution at Request Time

The system MUST resolve the current user's role for a given budget by querying `BudgetMembership`
at request time. This MUST NOT rely on any role stored in the JWT.

**Role hierarchy (descending privilege):** `owner` > `admin` > `operator` > `read-only`

**Soft-delete check (NEW):** Before membership lookup, the authorization handler MUST check whether
the target Budget has `IsDeleted = true`. If the Budget is soft-deleted, the handler MUST treat it
identically to a non-existent budget: set `httpContext.Items["budget-not-found"] = true` and return
HTTP 404. The membership cache entry (`budget-membership:{userId}:{budgetId}`) MUST NOT be populated
for a soft-deleted budget.

**Caching:** The authorization handler SHOULD use a short-TTL in-memory cache (keyed by
`userId + budgetId`, TTL ≤ 60 seconds) to avoid N+1 DB queries per request. Cache MUST be
invalidated when membership changes or the budget is soft-deleted/restored.

(Previously: AUTHZ-1 did not check `IsDeleted`; a soft-deleted budget was treated the same as an
active one — memberships were resolved normally and access was granted or denied by role.)

#### Scenario: Authorized request

- GIVEN user has `admin` role in budget `{id}` and budget is not soft-deleted
- WHEN a protected endpoint requiring minimum `admin` role is called
- THEN the request is allowed through

#### Scenario: Insufficient role

- GIVEN user has `operator` role in budget `{id}` and budget is not soft-deleted
- WHEN a protected endpoint requiring `admin` or higher is called
- THEN `403 Forbidden` is returned with error code `AUTH_INSUFFICIENT_ROLE`

#### Scenario: No membership

- GIVEN user has no `BudgetMembership` record for budget `{id}` and budget is not soft-deleted
- WHEN any protected budget endpoint is called
- THEN `403 Forbidden` is returned with error code `AUTH_NOT_A_MEMBER`

#### Scenario: JWT has no roles

- GIVEN a valid JWT with no role claims
- WHEN a protected budget endpoint is called
- THEN role resolution is performed exclusively via DB/cache lookup — the JWT role field is never consulted

#### Scenario: Soft-deleted budget returns 404 (NEW)

- GIVEN budget `{id}` has `IsDeleted = true`
- WHEN any protected budget endpoint is called (regardless of the caller's membership or role)
- THEN HTTP 404 is returned
- AND no membership cache entry is written for `budget-membership:{userId}:{budgetId}`

#### Scenario: Restored budget is accessible again (NEW)

- GIVEN budget `{id}` was soft-deleted and has now been restored (`IsDeleted = false`)
- AND any stale `budget-membership:{userId}:{budgetId}` cache entries have been evicted
- WHEN a protected budget endpoint is called by a member
- THEN the request is handled normally (200 or 403 by role, not 404)

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Role below required threshold | 403 | `AUTH_INSUFFICIENT_ROLE` |
| No membership for budget | 403 | `AUTH_NOT_A_MEMBER` |
| Budget is soft-deleted | 404 | (same path as budget-not-found) |
