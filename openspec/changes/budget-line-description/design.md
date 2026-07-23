# Design: BudgetLine Description Field

## Technical Approach

Add a nullable `Description` property (max 500 chars) to the `BudgetLine` entity and wire it through Create, Update, and List slices following existing VSA conventions. Simultaneously remove the dead `note` plumbing from line-level endpoints and frontend forms. Revision-scoped `note` in `UpdateBudgetLineRevision` and `BudgetLineCustomizationsView` remains untouched.

## Architecture Decisions

### Decision: Description on entity vs. separate table

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Nullable column on `BudgetLines` table | Simple, single join-free read, matches existing pattern for `Name` | **Chosen** |
| Separate `BudgetLineDescriptions` table | Over-engineered for a single optional text field | Rejected |

**Rationale**: `Description` is a stable, line-level attribute (like `Name`). No history/versioning required. A nullable column is the simplest path consistent with the existing schema.

### Decision: Domain method signature change strategy

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Add `description` parameter to `Create()` and `Update()` | Explicit, self-documenting, matches existing pattern | **Chosen** |
| Use a separate `SetDescription()` method | Extra method call in handler, diverges from established pattern | Rejected |

**Rationale**: Both `Create()` and `Update()` already accept all metadata fields inline. Adding `description` keeps the pattern uniform and avoids a two-step handler flow.

### Decision: Drop `note` from `ListBudgetLines` response

| Option | Tradeoff | Decision |
|--------|----------|----------|
| Remove `Note` from `BudgetLineResponse` and Dapper SQL | Breaking contract change, but `note` was revision data leaked to line level | **Chosen** |
| Keep `note` alongside `description` | Confusing semantics, frontend was sending `note` to a dead endpoint | Rejected |

**Rationale**: The exploration confirmed `note` on `BudgetLineResponse` comes from the revision join. Exposing it at the line level conflates line metadata with revision annotations. Clean break enforces the semantic split.

### Decision: Migration naming

| Option | Decision |
|--------|----------|
| `AddBudgetLineDescription` | **Chosen** - matches project convention (PascalCase verb + entity + field) |

Base migration: `20260722041748_WidenAuditLogActionColumn`.

## Data Flow

```
Create/Update Request
    |
    v
Command (+ Description?) --> Validator (MaxLength 500) --> Handler
    |                                                        |
    |  Create: BudgetLine.Create(..., description)           |
    |  Update: line.Update(..., description)                 |
    v                                                        v
AppDbContext --> EF Core --> PostgreSQL BudgetLines.Description

ListBudgetLines Request
    |
    v
Dapper SQL: SELECT bl."Description" FROM BudgetLines bl ...
    |        (no longer joins r."Note")
    v
BudgetLineResponse { Description }
    |
    v
Frontend BudgetLinesView (truncated column) / BudgetLineModal (textarea)
```

## File Changes

| File | Action | Description |
|------|--------|-------------|
| `SharedKernel/Entities/BudgetLine.cs` | Modify | Add `Description` property (string?, private set). Add `description` param to `Create()` and `Update()`. |
| `SharedKernel/Persistence/Configurations/BudgetLineConfiguration.cs` | Modify | Add `builder.Property(l => l.Description).HasMaxLength(500)` |
| `Migrations/{timestamp}_AddBudgetLineDescription.cs` | Create | ADD COLUMN `Description varchar(500) NULL` to `BudgetLines` |
| `Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineCommand.cs` | Modify | Add `string? Description` parameter |
| `Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineHandler.cs` | Modify | Pass `cmd.Description` to `BudgetLine.Create()` |
| `Features/BudgetStructure/CreateBudgetLine/CreateBudgetLineValidator.cs` | Modify | Add `RuleFor(x => x.Description).MaximumLength(500)` when not null |
| `Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineCommand.cs` | Modify | Add `string? Description` parameter |
| `Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineHandler.cs` | Modify | Pass `cmd.Description` to `line.Update()` |
| `Features/BudgetStructure/UpdateBudgetLine/UpdateBudgetLineValidator.cs` | Modify | Add `RuleFor(x => x.Description).MaximumLength(500)` when not null |
| `Features/BudgetStructure/ListBudgetLines/ListBudgetLinesQuery.cs` | Modify | Replace `Note` with `Description` in `BudgetLineResponse` record |
| `Features/BudgetStructure/ListBudgetLines/ListBudgetLinesHandler.cs` | Modify | Add `bl."Description"` to SQL SELECT; remove `r."Note"` from lateral join; update `BudgetLineRow` and mapping |
| `frontend/src/features/budget-structure/types.ts` | Modify | `BudgetLineResponse`: remove `note?`, add `description?`. `CreateBudgetLinePayload`: remove `note?`, add `description?`. `UpdateBudgetLinePayload`: remove `note?`, add `description?`. |
| `frontend/.../BudgetLineModal.vue` | Modify | Replace note textarea with description textarea (id=`line-description`, maxlength=500, `v-model="form.description"`). Update form reactive, handleSubmit payloads. |
| `frontend/.../BudgetLineRow.vue` | Modify | Replace `note` in form/display/inline-edit with `description`. Column shows truncated description. |
| `frontend/.../BudgetLinesView.vue` | Modify | Replace Note column header with Description. Replace `inlineAddForm.note` with `inlineAddForm.description`. Update inline-add payload. |
| `frontend/src/i18n/locales/en.json` | Modify | Add `"description": "Description"` key under `budgetLines`. Line-level `"note"` key remains (used by revisions section at line 237). |
| `frontend/src/i18n/locales/es.json` | Modify | Add `"description": "Descripcion"` key under `budgetLines`. |

## Interfaces / Contracts

```csharp
// Updated BudgetLine.Create signature
public static BudgetLine Create(
    Guid budgetId, Guid categoryGroupId, Guid? categoryId,
    string name, LineType lineType,
    DateOnly startDate, DateOnly? endDate,
    decimal initialAmount, Guid currencyId,
    int displayOrder = 0,
    string? description = null);

// Updated BudgetLine.Update signature
public void Update(
    Guid categoryGroupId, Guid? categoryId,
    string name, LineType lineType,
    string? description = null);

// Updated BudgetLineResponse
public sealed record BudgetLineResponse(
    Guid Id, Guid BudgetId, Guid CategoryGroupId, Guid? CategoryId,
    string Name, string LineType, int DisplayOrder,
    DateOnly StartDate, DateOnly? EndDate,
    decimal? BudgetedAmount, Guid? CurrencyId,
    string? CurrencyCode, string? CurrencySymbol,
    string? Description,              // NEW — replaces Note
    DateTimeOffset? DeletedAt = null);
```

```typescript
// Updated frontend types
interface BudgetLineResponse {
  // ... existing fields ...
  description?: string    // NEW
  // note removed
}

interface CreateBudgetLinePayload {
  // ... existing fields ...
  description?: string    // NEW
  // note removed
}

interface UpdateBudgetLinePayload {
  // ... existing fields ...
  description?: string    // NEW
  // note removed
}
```

## Testing Strategy

| Layer | What to Test | Approach |
|-------|-------------|----------|
| Unit | `BudgetLine.Create()` sets Description | In-memory EF, assert property |
| Unit | `BudgetLine.Update()` sets Description | In-memory EF, assert property |
| Unit | `CreateBudgetLineValidator` rejects >500 chars | Validator unit test |
| Unit | `UpdateBudgetLineValidator` rejects >500 chars | Validator unit test |
| Unit | `CreateBudgetLineHandler` persists description | Handler test with in-memory DB |
| Unit | `UpdateBudgetLineHandler` persists description | Handler test with in-memory DB |
| Unit | `BudgetLineModal` emits description in payload | @testing-library/vue |
| Unit | `BudgetLineRow` shows truncated description | @testing-library/vue |
| Integration | `ListBudgetLines` returns description, no note | Dapper query against test DB |

## Migration / Rollout

Single EF migration adding a nullable column. No data backfill needed — existing rows get `NULL` description, which the UI handles as empty/dash. Rollback: `dotnet ef migrations remove` or generate down migration dropping the column.

## Open Questions

None. All decisions are resolved based on codebase analysis.
