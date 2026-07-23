# Spec: budget-line-description

## Change Summary

Adds an optional `Description` field (max 500 chars) to the `BudgetLine` entity and propagates it through create, update, and list APIs and frontend surfaces. Simultaneously removes the dead `note` field from all line-level create/edit paths (backend command contracts, frontend types, forms, and list responses). Revision-scoped notes on `BudgetLineRevision` are left intact and are not in scope.

---

## Capability Index

| Capability | Change Type | Affected Slice / Component |
|---|---|---|
| `budget-structure` / BudgetLine entity | Modified | `SharedKernel/Entities/BudgetLine.cs` |
| `budget-structure` / DB schema | New | EF migration `AddBudgetLineDescription` |
| `budget-structure` / CreateBudgetLine | Modified | `Features/BudgetStructure/CreateBudgetLine/` |
| `budget-structure` / UpdateBudgetLine | Modified | `Features/BudgetStructure/UpdateBudgetLine/` |
| `budget-structure` / ListBudgetLines | Modified | `Features/BudgetStructure/ListBudgetLines/` |
| `budget-structure-ui` / BudgetLineModal | Modified | `frontend/.../BudgetLineModal.vue` |
| `budget-structure-ui` / BudgetLinesView | Modified | `frontend/.../BudgetLinesView.vue` |
| `budget-structure-ui` / BudgetLineRow | Modified | `frontend/.../BudgetLineRow.vue` |
| `budget-structure-ui` / Frontend types | Modified | `frontend/.../types.ts` |
| `budget-structure-ui` / i18n | Modified | `frontend/src/i18n/locales/en.json`, `es.json` |
| `budget-line-customizations` / BudgetLineCustomizationsView | Verify only | `frontend/.../BudgetLineCustomizationsView.vue` |

---

## Requirements

### REQ-BLD-01 — BudgetLine.Description property

**What must be true after the change:**

- `BudgetLine` entity has a nullable `Description` property of type `string?`.
- The domain enforces a maximum of 500 characters. Values exceeding 500 chars are rejected before persistence.
- `null` and empty string are both accepted (field is optional).
- The EF column configuration matches: `HasColumnName("Description")`, `HasMaxLength(500)`, `.IsRequired(false)`.
- A new EF Core migration exists that adds the `Description` column (nullable, varchar 500) to the `BudgetLines` table.
- The `BudgetLine.Create()` factory method accepts an optional `description` parameter and assigns it.
- The `BudgetLine.Update()` (or equivalent domain mutator) accepts an optional `description` parameter and updates the property.

**What must NOT be true:**

- `BudgetLine` has no `Note` property.
- No `Note` column is added to the `BudgetLines` table by this change.

---

### REQ-BLD-02 — CreateBudgetLine endpoint

**What must be true after the change:**

- `CreateBudgetLineCommand` includes an optional `Description string?` parameter.
- `CreateBudgetLineValidator` enforces: if `Description` is not null, its length must not exceed 500 chars.
- `CreateBudgetLineHandler` passes `description` to `BudgetLine.Create()` and persists the result.
- The endpoint accepts `description` in the request body.
- The `note` parameter is absent from `CreateBudgetLineCommand`, its validator, and its handler.

**What must NOT be true:**

- The command or endpoint accepts `note`.
- A `note` value sent in the JSON body is mapped to any field on the created entity.

---

### REQ-BLD-03 — UpdateBudgetLine endpoint

**What must be true after the change:**

- `UpdateBudgetLineCommand` includes an optional `Description string?` parameter.
- `UpdateBudgetLineValidator` enforces: if `Description` is not null, its length must not exceed 500 chars.
- `UpdateBudgetLineHandler` passes `description` to the domain mutator and persists the change.
- The endpoint accepts `description` in the request body.
- The `note` parameter is absent from `UpdateBudgetLineCommand`, its validator, and its handler.

**What must NOT be true:**

- The command or endpoint accepts `note`.
- The update operation touches `BudgetLineRevision.Note` in any way.

---

### REQ-BLD-04 — ListBudgetLines response

**What must be true after the change:**

- The Dapper SQL query in `ListBudgetLinesHandler` selects `bl."Description"` from the `BudgetLines` table.
- `BudgetLineResponse` (or the Dapper row record) has a `Description string?` property.
- The API response for each budget line item includes `description` (may be `null`).
- The `note` property (previously read from the current revision) is absent from the SQL query projection and from `BudgetLineResponse`.

**What must NOT be true:**

- The response includes a `note` field sourced from `BudgetLineRevision`.
- The Dapper row type has a `Note` property.

---

### REQ-BLD-05 — BudgetLineCustomizationsView — note field (verify only)

**What must be true (no code change expected unless a gap is found):**

- The inline-edit form in `BudgetLineCustomizationsView.vue` exposes a `note` input bound to the `UpdateBudgetLineRevision` payload.
- The modal form in `BudgetLineCustomizationsView.vue` exposes a `note` input bound to the `UpdateBudgetLineRevision` payload.
- `UpdateBudgetLineRevision` endpoint continues to accept and persist `note` without modification.
- No test regression is introduced in `BudgetLineCustomizationsView` tests by this change.

**If a gap is found during implementation:**

- Surface it as a separate task. Do not widen the scope of this change to fix it.

---

### REQ-BLD-06 — BudgetLinesView table — Description column

**What must be true after the change:**

- `BudgetLinesView.vue` renders a `Description` column in the budget lines table.
- The column displays `description` truncated to approximately 80-100 visible characters when longer. The truncation strategy (CSS or JS) is an implementation detail, but truncation must not corrupt the value.
- The full `description` text is accessible in the create/edit modal (via `BudgetLineModal`).
- The previous `Note` column is absent from the table.
- The inline-add row in `BudgetLinesView` (if it exists) does not show a `note` input.

---

### REQ-BLD-07 — BudgetLineModal — Description textarea

**What must be true after the change:**

- `BudgetLineModal.vue` contains a `<textarea>` (or equivalent multi-line input) bound to `description`.
- The textarea enforces `maxlength="500"` at the HTML level.
- The textarea is optional — submitting the modal with an empty `description` is valid.
- The `note` field (input or textarea) is absent from `BudgetLineModal.vue`.
- The i18n key used for the label resolves to the Description i18n entry (REQ-BLD-09).

---

### REQ-BLD-08 — Frontend TypeScript types

**What must be true after the change:**

- `BudgetLineResponse` interface (or Zod schema equivalent) includes `description?: string` and does NOT include `note?: string`.
- `CreateBudgetLinePayload` interface (or Zod schema equivalent) includes `description?: string` and does NOT include `note?: string`.
- `UpdateBudgetLinePayload` interface (or Zod schema equivalent) includes `description?: string` and does NOT include `note?: string`.
- All usages of these types compile without TypeScript errors after the change.

**What must NOT be true:**

- Any of the three types above carries a `note` property.
- A `note` property is set anywhere in the composable or store that calls create/update budget line endpoints.

---

### REQ-BLD-09 — i18n keys

**What must be true after the change:**

- `frontend/src/i18n/locales/en.json` contains an i18n key for `description` under the budget line namespace (e.g., `budgetLine.description` or equivalent).
- `frontend/src/i18n/locales/es.json` contains the equivalent Spanish translation for the same key.
- BudgetLine-level `note` i18n keys (keys that were used in the create/edit modal or table header for the line-level note) are removed from both locale files.
- Revision-level `note` i18n keys (if any, used in `BudgetLineCustomizationsView`) are left unchanged.

---

## Edge Cases

| ID | Scenario | Expected behaviour |
|---|---|---|
| EC-01 | `description` is exactly 500 characters | Accepted by validator and persisted |
| EC-02 | `description` is 501 characters | Rejected by `CreateBudgetLine`/`UpdateBudgetLine` validator; HTTP 422 returned |
| EC-03 | `description` is `null` | Accepted; stored as NULL in DB; `description: null` returned in `ListBudgetLines` |
| EC-04 | `description` is empty string `""` | Accepted; stored as empty or NULL depending on domain choice (document the choice); returned as-is |
| EC-05 | `note` is passed in create/update request body | Silently ignored (no mapping exists); does NOT cause an error |
| EC-06 | Existing `BudgetLine` rows after migration | `description` column is NULL; no data loss; `ListBudgetLines` returns `description: null` for these rows |
| EC-07 | `description` contains only whitespace | Accepted (no trimming enforced at domain level by this spec); display may trim for UX |
| EC-08 | `BudgetLineCustomizationsView` note save | Continues to persist `BudgetLineRevision.Note` correctly; not affected by this change |
| EC-09 | Table truncation renders partial unicode / emoji | Truncation must not split multi-byte characters visibly; CSS `text-overflow: ellipsis` is safe |
| EC-10 | TypeScript strict mode — `description?` optional vs. `description: string \| undefined` | Either is acceptable; the form must handle both `undefined` and `null` from the API gracefully |

---

## Acceptance Scenarios

### Scenario 1 — Create line with description (happy path)

**Given** a valid `CreateBudgetLine` request with `description: "Monthly salary cost for engineering team"` and no `note` field  
**When** the endpoint processes the command  
**Then** a new `BudgetLine` row is created with `Description = "Monthly salary cost for engineering team"` and no error is returned

### Scenario 2 — Create line, description exceeds 500 chars

**Given** a `CreateBudgetLine` request with `description` of 501 characters  
**When** the endpoint processes the command  
**Then** HTTP 422 is returned; the error references the `description` field; no row is created

### Scenario 3 — Create line without description

**Given** a `CreateBudgetLine` request with no `description` field (or `description: null`)  
**When** the endpoint processes the command  
**Then** a new `BudgetLine` row is created with `Description = null` and no error is returned

### Scenario 4 — List budget lines includes description, excludes note

**Given** one or more `BudgetLine` rows exist, some with a description, some without  
**When** `ListBudgetLines` is called  
**Then** each item in the response includes `description` (string or null); no item includes a `note` property sourced from the revision

### Scenario 5 — Update line description

**Given** an existing `BudgetLine` with `Description = null`  
**When** `UpdateBudgetLine` is called with `description: "Updated purpose"`  
**Then** the row is updated with `Description = "Updated purpose"` and the response reflects the new value

### Scenario 6 — Frontend modal removes note, adds description

**Given** the `BudgetLineModal` is open in create mode  
**When** the user inspects the form fields  
**Then** there is a "Description" textarea (max 500 chars) and no "Note" input

### Scenario 7 — Table shows description column, not note

**Given** `BudgetLinesView` is rendered with lines that have descriptions  
**When** the table is displayed  
**Then** a "Description" column exists and shows truncated text; no "Note" column exists

### Scenario 8 — Long description is truncated in table, full in modal

**Given** a `BudgetLine` with a `description` of 300 characters  
**When** the table renders the line  
**Then** the cell shows approximately the first 80-100 characters followed by an ellipsis (or equivalent); opening the edit modal shows the full 300-character text in the textarea

### Scenario 9 — Revision note unaffected in customizations view

**Given** `BudgetLineCustomizationsView` is open for a line  
**When** the user edits the inline or modal revision form  
**Then** a `note` field is present and saving it calls `UpdateBudgetLineRevision` with the note value; no regression

### Scenario 10 — Existing rows after migration

**Given** the migration has run against a database with existing `BudgetLine` rows  
**When** `ListBudgetLines` is called  
**Then** each pre-existing line returns `description: null`; no other column is affected

### Scenario 11 — TypeScript types exclude note

**Given** the updated frontend TypeScript types  
**When** the developer attempts to set `payload.note = "x"` on a `CreateBudgetLinePayload`  
**Then** TypeScript compilation fails with a type error

---

## Non-Goals

- Full-text search or filtering budget lines by `description`.
- `Description` history or audit trail.
- `BudgetLineRevision.Note` field changes — revision notes are out of scope.
- Changes to `BudgetLineCustomizationsView` logic (verify only; fix only if gap found).
- Backend i18n / `.resx` changes for `description` — the field label lives in the frontend only.
- Migration rollback automation — documented in the proposal; not encoded in this spec.
- Display-level HTML sanitization of `description` (plain text only; no HTML rendering expected).

---

## Out of Scope

- Any other `BudgetLine` fields not mentioned above.
- `BudgetLineRevision` entity, `UpdateBudgetLineRevision` slice, and `BudgetLineCustomizationsView` (except the verification requirement in REQ-BLD-05).
- Gateway, auth, observability, or caching changes.
- Database-level full-text index on `Description`.
- Multi-language description (the field is a single string; no locale-per-description model).
