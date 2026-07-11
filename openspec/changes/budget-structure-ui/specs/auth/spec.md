# Delta for auth

## ADDED Requirements

### Requirement: REG-I18N-1 — Language Label i18n Key

The register view MUST use the i18n key `auth.register.languageLabel` for the language selector label. No hardcoded label text MAY appear at `RegisterView.vue:152` or any equivalent location.

#### Scenario: Language label rendered from i18n

- GIVEN locale is "es"
- WHEN the register view renders
- THEN the language selector label shows the Spanish translation for `auth.register.languageLabel`
- AND no hardcoded "Language" string is present in the DOM

---

## MODIFIED Requirements

### Requirement: LOGIN-1 — Credential Verification and Token Issuance

The system MUST verify credentials, issue a JWT access token (15-minute TTL) and a rotating refresh token (7-day TTL, single-use, hashed in DB), and update `LastLoginAt`. The `auth.login.emailPlaceholder` i18n key MUST escape `@` as `{'@'}` to prevent vue-i18n linked-message errors.

(Previously: emailPlaceholder value contained a bare `@` character, causing a vue-i18n runtime linked-message warning.)

**Field validation rules:**
| Field | Rule |
|---|---|
| `email` | REQUIRED. Valid email format. |
| `password` | REQUIRED. Non-empty. |

**Response on success:** `200 OK` with `{ accessToken, refreshToken, expiresIn: 900, user: { id, email, firstName, lastName, preferredLocale } }`.

#### Scenario: Happy path — valid credentials

- GIVEN a user exists with `email` and a matching BCrypt hash
- WHEN `POST /api/auth/login` is called with correct credentials
- THEN `200 OK` is returned with a fresh `accessToken` (TTL 15 min) and `refreshToken` (TTL 7 days)
- AND `LastLoginAt` is updated
- AND the `refreshToken` value is stored hashed in `RefreshToken` table

#### Scenario: Wrong password

- GIVEN a user exists with `email = "user@example.com"`
- WHEN `POST /api/auth/login` is called with an incorrect password
- THEN `401 Unauthorized` is returned with error code `AUTH_INVALID_CREDENTIALS`
- AND the response time MUST NOT reveal whether the email exists (constant-time comparison)

#### Scenario: Unknown email

- GIVEN no account exists with `email = "ghost@example.com"`
- WHEN `POST /api/auth/login` is called
- THEN `401 Unauthorized` is returned with error code `AUTH_INVALID_CREDENTIALS`

#### Scenario: Missing field

- GIVEN a login request with `password` omitted
- WHEN `POST /api/auth/login` is called
- THEN `422 Unprocessable Entity` is returned with field error `password: FIELD_REQUIRED`

#### Scenario: Email placeholder renders without vue-i18n warning

- GIVEN the login view renders with locale "en"
- WHEN the email input placeholder is displayed
- THEN no vue-i18n linked-message warning appears in the browser console

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Wrong email or password | 401 | `AUTH_INVALID_CREDENTIALS` |
| Field missing/invalid | 422 | `FIELD_REQUIRED` / `FIELD_INVALID` |

**i18n keys (frontend `en.json` / `es.json`):** `auth.login.title`, `auth.login.emailPlaceholder`, `auth.login.passwordPlaceholder`, `auth.login.submit`, `auth.login.registerLink`, `auth.login.error.invalidCredentials`

---

### Requirement: REG-1 — Account Creation

The system MUST create a user account, hash the password with BCrypt (workFactor 12), persist a default budget, and return a JWT pair on success. The `auth.register.emailPlaceholder` i18n key MUST escape `@` as `{'@'}` to prevent vue-i18n linked-message errors.

(Previously: emailPlaceholder value contained a bare `@` character, causing a vue-i18n runtime linked-message warning.)

**Field validation rules:**
| Field | Rule |
|---|---|
| `email` | REQUIRED. Valid email format (RFC 5322). Max 254 chars. Case-insensitive unique. |
| `password` | REQUIRED. Min 8 chars, max 72 chars. Must contain at least 1 uppercase, 1 lowercase, 1 digit. |
| `firstName` | REQUIRED. Min 1 char, max 100 chars. Trimmed. |
| `lastName` | REQUIRED. Min 1 char, max 100 chars. Trimmed. |
| `preferredLocale` | OPTIONAL. If provided, must be `"en"` or `"es"`. Defaults to `"en"`. |

**Post-registration side effects (all atomic in same transaction):**
- A `Budget` record MUST be created with `Name = "{firstName}'s Budget"`, `OwnerId = newUser.Id`.
- A `BudgetMembership` record MUST be created with `Role = owner`, linking the new user to the new budget.

**Response on success:** `201 Created` with `{ accessToken, refreshToken, expiresIn, user: { id, email, firstName, lastName, preferredLocale } }`.

#### Scenario: Happy path — valid registration

- GIVEN no account exists with the provided email
- WHEN `POST /api/auth/register` is called with valid `email`, `password`, `firstName`, `lastName`
- THEN a `User`, `Budget`, and `BudgetMembership (owner)` are created in one transaction
- AND a `201` response is returned with `accessToken`, `refreshToken`, and user profile
- AND the refresh token is stored as a BCrypt hash in `RefreshToken` table

#### Scenario: Duplicate email

- GIVEN an account already exists with `email = "user@example.com"`
- WHEN `POST /api/auth/register` is called with the same email (any casing)
- THEN the system returns `409 Conflict` with error code `AUTH_EMAIL_TAKEN`

#### Scenario: Password too weak

- GIVEN a registration request with `password = "abc123"` (no uppercase)
- WHEN `POST /api/auth/register` is called
- THEN the system returns `422 Unprocessable Entity` with field error `password: AUTH_PASSWORD_TOO_WEAK`

#### Scenario: Missing required field

- GIVEN a registration request with `firstName` omitted
- WHEN `POST /api/auth/register` is called
- THEN the system returns `422 Unprocessable Entity` with field error `firstName: FIELD_REQUIRED`

#### Scenario: Invalid preferredLocale

- GIVEN a registration request with `preferredLocale = "fr"`
- WHEN `POST /api/auth/register` is called
- THEN the system returns `422 Unprocessable Entity` with field error `preferredLocale: AUTH_LOCALE_UNSUPPORTED`

#### Scenario: Email placeholder renders without vue-i18n warning

- GIVEN the register view renders with locale "es"
- WHEN the email input placeholder is displayed
- THEN no vue-i18n linked-message warning appears in the browser console

**Error responses:**
| Condition | HTTP | Error Code |
|---|---|---|
| Email already taken | 409 | `AUTH_EMAIL_TAKEN` |
| Password too weak | 422 | `AUTH_PASSWORD_TOO_WEAK` |
| Field missing/invalid | 422 | `FIELD_REQUIRED` / `FIELD_INVALID` |
| Locale not supported | 422 | `AUTH_LOCALE_UNSUPPORTED` |

**i18n keys (frontend `en.json` / `es.json`):** `auth.register.title`, `auth.register.emailPlaceholder`, `auth.register.passwordPlaceholder`, `auth.register.firstNamePlaceholder`, `auth.register.lastNamePlaceholder`, `auth.register.submit`, `auth.register.loginLink`, `auth.register.successMessage`, `auth.register.languageLabel`
