# Proposal: Budget Line Redesign

## Intent

BudgetLine is currently scoped to a Period, requiring users to manually duplicate lines across periods and manage a fragile `IsRecurring` flag that has no real date semantics. This causes:

- **Manual duplication pain**: creating the same line in every period of a cycle.
- **No cross-cycle reuse**: lines die with their period; no way to express "this line is valid from March to forever."
- **Fragile `IsRecurring` flag**: a boolean with no enforcement -- it hints at intent but the system does not act on it.
- **Incorrect cascade semantics**: deleting a Period or Cycle cascades to BudgetLines, which is wrong when lines are a budget-level concept.

The redesign promotes BudgetLine to a Budget-level entity with explicit `StartDate`/`EndDate` validity and a gapless revision system for amounts, replacing per-period duplication with date-range semantics.

## Scope

### In Scope

- **Entity model**: remove `PeriodId`, `IsRecurring` from `BudgetLine`; add `StartDate`, `EndDate`; replace `RevisedAt` on `BudgetLineRevision` with `ValidFrom`/`ValidTo`; remove `Period.BudgetLines` nav
- **Domain method**: `BudgetLine.SplitRevision()` for gapless revision splitting
- **EF configurations**: new indexes, FK changes, unique constraint `UNIQUE(BudgetId, Name)`
- **6 BudgetStructure slices**: Create, Update, Delete, Restore, List, Reorder -- all lose `PeriodId` scope
- **3 BudgetExecution slices**: CreateExecutionRecord (new date-range guard), ListPeriodExecutionTotals (date-intersection SQL), ListExecutionRecords (remove Period JOIN)
- **3 cascade handlers**: DeletePeriod, DeleteCycle, RestoreCycle -- remove BudgetLine cascade
- **Frontend**: API layer, types, store, BudgetLinesView, BudgetLineModal, BudgetLineRow, BudgetMatrixView -- remove `periodId` params, `isRecurring` fields; add date range UI
- **All BudgetLine tests**: unit + integration rewrite (entity, handler, endpoint)
- **Migration**: fresh EF migration with data wipe (dev only)

### Out of Scope

- Historical data migration (dev wipe only)
- New UI for revision history visualization
- Budget-level date range configuration
- ExecutionRecord schema changes (PeriodId stays)
- Auth/role changes
- i18n key additions beyond field labels (`startDate`, `endDate`)

## Capabilities

### New Capabilities

- None

### Modified Capabilities

- `budget-structure`: BudgetLine entity model, CRUD slices, uniqueness constraint, cascade behavior all change
- `budget-execution`: period-amount resolution via date-range intersection; execution validation via date coverage
- `budget-structure-ui`: form fields, matrix grid, store/API signatures all change

## Approach

1. **Entity-first**: modify `BudgetLine` and `BudgetLineRevision` entities, add `SplitRevision()` domain method with unit tests
2. **EF config + migration**: update configurations, generate fresh migration (data wipe)
3. **Backend slices**: update all 9 affected slices (6 structure + 3 execution) with new signatures, validators, and handlers
4. **Frontend**: update types, API layer, store, then components (bottom-up)
5. **Test rewrite**: rewrite all broken tests; add gapless-split, date-coverage, and uniqueness scenarios

All work on `feat/budget-line-redesign` branch.

## Affected Areas

| Area | Impact | Description |
|------|--------|-------------|
| `SharedKernel/Entities/BudgetLine.cs` | Modified | Remove PeriodId/IsRecurring; add StartDate/EndDate; add SplitRevision() |
| `SharedKernel/Entities/BudgetLineRevision.cs` | Modified | Replace RevisedAt with ValidFrom/ValidTo |
| `SharedKernel/Entities/Period.cs` | Modified | Remove BudgetLines navigation |
| `SharedKernel/Persistence/Configurations/` | Modified | 3 EF configs updated |
| `Features/BudgetStructure/*/` | Modified | 6 slices lose PeriodId scope |
| `Features/BudgetExecution/*/` | Modified | 3 slices: date-range validation/SQL |
| `frontend/src/features/budget-structure/` | Modified | API, types, store, 3 components |
| `frontend/src/features/budget-execution/` | Modified | MatrixView, execution store |
| `tests/` | Modified | 15+ test files rewritten |

## Risks

| Risk | Likelihood | Mitigation |
|------|------------|------------|
| Gapless revision invariant violated by concurrent edits | Med | Domain method encapsulates all split logic; unit tests cover edge cases; `validFrom >= today` prevents retroactive conflicts |
| ListPeriodExecutionTotals SQL complexity increases significantly | High | Date-intersection JOIN is well-defined; covered by integration tests with multi-period fixtures |
| Large PR size exceeds 400-line review budget | High | Chained PRs: (1) entity+migration, (2) backend slices, (3) frontend, (4) tests |

## Rollback Plan

- Revert the feature branch. Fresh migration means no backward data migration needed (dev wipe policy).
- If partially merged via chained PRs, revert in reverse order.

## Dependencies

- None. All changes are internal to the existing codebase.

## Success Criteria

- [ ] BudgetLine exists at Budget level with StartDate/EndDate, no PeriodId
- [ ] Gapless revision system: SplitRevision produces non-overlapping, gap-free date ranges
- [ ] Period amount resolution returns exactly one revision per active line per period
- [ ] Matrix grid correctly shows enabled/disabled cells based on date coverage
- [ ] `IsRecurring` fully removed from entity, API, and frontend
- [ ] All unit and integration tests pass
- [ ] No cascade from Period/Cycle delete to BudgetLines
