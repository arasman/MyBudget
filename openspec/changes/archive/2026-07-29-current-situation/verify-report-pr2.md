# Verify Report: current-situation — PR2 Frontend (Phases 5–7) — Re-verify after UX post-commit

## Metadata

| Field | Value |
|---|---|
| Change | current-situation |
| PR Scope | PR2 Frontend (phases 5–7) |
| Original verify | 2026-07-28 (obs #373) |
| Re-verify | 2026-07-29 |
| Commit verified | ae21224 |
| Verdict | PASS WITH WARNINGS |
| Strict TDD | Active |
| Unit tests | 428/428 passed (runtime confirmed) |
| Test files | 56 |

---

## Task Completeness

All Phase 5–7 tasks marked `[x]`. No unchecked tasks. Task completion: **COMPLETE**.

| Phase | Tasks | Status |
|---|---|---|
| Phase 5: BankAccount frontend | 5.1–5.9 | All checked |
| Phase 6: CurrentSituation frontend | 6.1–6.15 | All checked |
| Phase 7: E2E tests | 7.1–7.5 | All checked |

---

## Test Suite Evidence

Command: `npx vitest run --reporter=verbose`
Exit code: 0
Test files: 56 passed (56)
Tests: 428 passed (428)
Duration: 90.06s

---

## Re-verify Scope: UX Changes in commit ae21224

### Check 1 — Computed value correctness (CutTotalsPanel.vue)

**Spec CS-6**: TotalDeudaEnCurso = Remaining + TotalCuentasQueRestan

**liveTotals in CutRecordForm.vue (line 203)**:
```
const totalDeudaEnCurso = props.remaining + totalNegative
```
`totalNegative` = SUM(BalanceInPrimary) for isPositive=false accounts. This matches CS-6: `TotalCuentasQueRestan + Remaining`.

**totalAvailable (CutTotalsPanel.vue, line 97)**:
```
const totalAvailable = computed(() => props.totals.totalPositive)
```
Spec context: "Total Available = totalPositive". CORRECT.

**totalNet (CutTotalsPanel.vue, line 99)**:
```
const totalNet = computed(() => props.totals.totalPositive - props.totals.totalDeudaEnCurso)
```
Spec context: "Total Net = totalPositive - totalDeudaEnCurso". CORRECT.

**Sign display**: negative rows (liabilities, budget commitment, total debt) render with `- {{ formatAmount(...) }}` prefix, positive rows render plain. CORRECT.

**Status: PASS**

---

### Check 2 — NaN guard consistency (CutRecordForm.vue)

`safeBalance(id)` (line 163–166):
```ts
function safeBalance(id: string): number {
  const val = balances.value[id]
  return Number.isFinite(val) ? val : 0
}
```

Used in:
- `formatBalanceDisplay`: calls `safeBalance` — PASS
- `onBalanceFocus`: calls `safeBalance` — PASS
- `liveTotals` computed (line 196–199): calls `safeBalance(acc.bankAccountId)` per account — PASS
- `handleSave` (line 222–224): `Number.isFinite(balance) ? balance : 0` applied directly on the map entries — PASS

Guard is consistent across all four uses. **Status: PASS**

---

### Check 3 — Save draft bug fix (selectedDate.value vs store.currentDate)

`handleSave` in view (line 158–167):
```ts
async function handleSave(payload: ...): Promise<void> {
  if (!selectedDate.value) return
  await store.upsertCutRecord(budgetId.value, selectedDate.value, payload)
}
```

`selectedDate` is a local `ref<string>('')` that is:
- Set to `record.cutDate` when a new record loads (watch on store.currentRecord, line 143)
- Set via date-change handler (line 171)
- Reset to last record's date on strategy cancel (line 183)

For drafts, `store.currentDate` returns `cutDates.value[currentDateIndex.value]`. For a draft (date not in cutDates), `currentDateIndex = -1`, so `store.currentDate = null`. Using `selectedDate.value` avoids this null — the fix is correct.

Null-guard at line 162 (`if (!selectedDate.value) return`) prevents saving if the ref was never populated. The only way this occurs is before `onMounted` completes, which is correct.

**Status: PASS**

---

### Check 4 — liveExchangeRate prop chain

Chain: form emits `update:liveExchangeRate` → view stores → CutTotalsPanel prop.

**Form emits** (CutRecordForm.vue, lines 143–149):
```ts
watch(localExchangeRate, (rate) => {
  const safeRate = Number.isFinite(rate) && rate > 0 ? rate : 1
  emit('update:liveExchangeRate', safeRate)
}, { immediate: true })
```
Emits on every change, immediately on mount.

**View wires** (CurrentSituationView.vue, line 53):
```
@update:live-exchange-rate="liveExchangeRate = $event"
```
`liveExchangeRate` initialized to `1` (line 132), reset to `record?.exchangeRate ?? 1` when record loads (line 141).

**CutTotalsPanel receives** (line 61):
```
:exchange-rate="liveExchangeRate"
```

**CutTotalsPanel uses it** to compute `safeExchangeRate` and `hasAltRate`, and divides executionSummary fields by `safeExchangeRate` for alt column.

Chain is correctly wired end-to-end. **Status: PASS**

---

### Check 5 — triggerSave exposure

**Form** (CutRecordForm.vue, line 229):
```ts
defineExpose({ triggerSave: handleSave })
```

**View ref** (line 127):
```ts
const formRef = ref<InstanceType<typeof CutRecordForm> | null>(null)
```
Typed with `InstanceType<typeof CutRecordForm>` — TypeScript will infer the exposed `triggerSave` method from `defineExpose`.

**View usage** (line 76):
```
@click="formRef?.triggerSave()"
```
Optional chaining prevents crash if ref is null (e.g., during loading/empty state — the button is only rendered inside `v-else` which requires `store.currentRecord` to be set, so formRef will be populated). Safe.

**Status: PASS**

---

### Check 6 — i18n keys

**New keys added (`totals` section, both locales)**:
- `totalAvailable`: EN "Total Available", ES "Total Disponible" — PRESENT
- `totalNet`: EN "Total Net", ES "Posición Neta" — PRESENT
- `deudaEnCurso`: EN "Total Debt", ES "Deuda Total" — PRESENT (key unchanged from original `deudaEnCurso`)

**Renamed value (key kept)**:
- `executionSummary.remaining`: EN "Budget Commitment", ES "Compromiso Presupuestario" — key `remaining` kept, display label updated — CORRECT

Note: the context description mentioned renaming key `remaining` → `budgetCommitment`. The actual implementation kept the key `remaining` but changed its display value to "Budget Commitment" / "Compromiso Presupuestario". The template references `t('currentSituation.executionSummary.remaining')` which resolves correctly. No broken references found.

**Status: PASS**

---

### Check 7 — DeleteCutModal daisyUI v5 fix

`DeleteCutModal.vue` uses `flex flex-col gap-1` pattern for label-above-input (lines 9–18), `w-full` on the input. Correct daisyUI v5 pattern (no `form-control` class). **Status: PASS**

---

## W-001 Status Update

Original W-001 (missing BankAccounts tab) from obs #373 was a pre-existing warning. This re-verify did not address it. The tab may have been added in a separate commit — not confirmed. Carries forward as WARNING unless explicitly resolved.

---

## Issues

### W-001 — WARNING (carry-forward): BankAccounts UI route not in BudgetTabs
**Spec**: CS-8 requires bank account management accessible from budget configuration, not exclusively from cut form.
**Finding**: Needs confirmation whether a BankAccounts tab exists in BudgetTabs.vue after post-verify commits. Not re-inspected in this pass; was unresolved at last verify.
**Remediation**: Confirm in archive phase or add tab if still missing.

### W-002 — SUGGESTION: Alt-currency column always shows when exchangeRate !== 1
**Finding**: `hasAltRate = computed(() => safeExchangeRate.value !== 1)`. If the budget has no alternate currency but a user types any non-1 exchange rate, the USD column appears. This may be confusing for single-currency budgets.
**Impact**: Low — cosmetic only. No data integrity issue.
**Remediation**: Could gate `hasAltRate` on whether the budget has an alternate currency configured, rather than purely on the exchange rate value.

### W-003 — WARNING (carry-forward): No browser-level E2E page navigation test
As documented in obs #373. Unchanged.

---

## Spec Compliance Matrix (re-verify scope)

| Requirement | Evidence | Status |
|---|---|---|
| CS-6: TotalDeudaEnCurso = Remaining + TotalNegative | liveTotals computed: `remaining + totalNegative` | PASS |
| CS-6: TotalAvailable = TotalPositive | `computed(() => props.totals.totalPositive)` | PASS |
| CS-6: TotalNet = TotalPositive - TotalDeudaEnCurso | `computed(() => totals.totalPositive - totals.totalDeudaEnCurso)` | PASS |
| CS-5: BalanceInPrimary via exchangeRate | `toBalanceInPrimary`: primary → identity, alt → balance × er | PASS |
| CS-7: Save button wired to form | `formRef?.triggerSave()` → `defineExpose({ triggerSave: handleSave })` | PASS |
| CS-7: i18n ES+EN all new keys | totalAvailable, totalNet, deudaEnCurso, remaining label all present in both locales | PASS |
| CS-4: Delete modal confirmation | `DeleteCutModal.vue` unchanged, daisyUI v5 layout fix only | PASS |

---

## Final Verdict: PASS WITH WARNINGS

- **CRITICALs**: 0
- **WARNINGs**: 2 (W-001 carry-forward BankAccounts tab, W-003 carry-forward no browser E2E)
- **SUGGESTIONs**: 1 (W-002 alt-currency column guard)
- **Tasks**: All 5.1–7.5 checked complete
- **Tests**: 428/428 unit passing (runtime confirmed, exit 0)

All 7 re-verify checks PASS. No regressions introduced by commit ae21224. Safe to proceed to sdd-archive.
