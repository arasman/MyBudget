# Auth Feature Specification

## Purpose

This spec covers all authentication and authorization behavior introduced by the `auth` change.
It defines what the system MUST do — not how to implement it.
All 7 capabilities are new; no existing auth spec exists.

---

## Shared Constraints

- SC-1: The application MUST fail to start if `JWT__Key` is not configured in the environment (User Secrets in dev, env var in prod). It MUST NOT fall back to a default value.
- SC-2: `JWT__Key` MUST NOT appear in `appsettings.json` or any committed configuration file.
- SC-3: The EF Core migration `AddAuthTables` MUST be the only migration that touches auth tables. `InitialCreate` MUST NOT be modified.
- SC-4: All JWT tokens MUST contain only: `sub` (userId), `email`, `jti`, `iat`, `exp`. No roles, no budget IDs.
- SC-5: Budget roles are NEVER baked into JWT — they are resolved at request time from `BudgetMembership`.

See full spec at openspec/specs/auth/spec.md (merged into main specs on 2026-07-08).
