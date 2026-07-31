import type { APIRequestContext } from '@playwright/test'
import { test, expect } from '@playwright/test'
import { seedBudgetCtx, createBankAccount, GTQ_CURRENCY_ID, type BudgetCtx } from './helpers'

/**
 * E2E: Cut totals snapshot-at-save-time semantics.
 * Spec: CS-6 "Snapshot unaffected by later data changes" (E2E level).
 *
 * Flow: save a cut with bank account balances (which also computes and
 * persists the execution-derived totals from the active period), capture
 * the totals payload the frontend renders in CutTotalsPanel right after
 * save, then mutate an execution record in the active period, reload the
 * cut, and assert the totals payload is byte-for-byte identical.
 *
 * Uses the API request fixture (no browser UI) to match the established
 * pattern in this directory (cut-record-create/delete/navigation.spec.ts):
 * CutTotalsPanel renders `totals` + `executionSummary` from the
 * GetCutRecord response verbatim (CurrentSituationView.vue passes
 * `store.currentRecord.totals` / `.executionSummary` straight through as
 * props after a reload — no client-side recomputation), so asserting on
 * the GET response is equivalent to asserting on what CutTotalsPanel
 * displays.
 */

const TOTALS_TITLE_URL = (budgetId: string, date: string) =>
  `/api/budgets/${budgetId}/cut-records/${date}`

/** Creates a category group + budget line, then an ExecutionRecord in the active period. */
async function seedExecution(
  request: APIRequestContext,
  ctx: BudgetCtx,
  operationDate: string,
): Promise<{ periodId: string; lineId: string; executionId: string }> {
  const headers = ctx.headers
  const budgetId = ctx.budgetId

  const groupResp = await request.post(`/api/budgets/${budgetId}/category-groups`, {
    headers,
    data: { name: 'Snapshot Test Group', displayOrder: 1 },
  })
  expect(groupResp.status()).toBe(201)
  const { id: groupId } = await groupResp.json()

  const lineResp = await request.post(`/api/budgets/${budgetId}/lines`, {
    headers,
    data: {
      name: 'Snapshot Test Line',
      lineType: 'Expense',
      categoryGroupId: groupId,
      startDate: '2026-01-01',
      endDate: null,
      initialAmount: 1000,
      currencyId: GTQ_CURRENCY_ID,
    },
  })
  expect(lineResp.status()).toBe(201)
  const { id: lineId } = await lineResp.json()

  const execResp = await request.post(
    `/api/budgets/${budgetId}/periods/${ctx.periodId}/budget-lines/${lineId}/executions`,
    {
      headers,
      data: {
        entryType: 1,
        amount: 200,
        note: 'Snapshot test execution',
        operationDate,
        currencyId: GTQ_CURRENCY_ID,
        exchangeRate: null,
        exchangeRateTo: null,
        accountId: null,
        paymentMethodId: null,
      },
    },
  )
  expect(execResp.status()).toBe(201)
  const { id: executionId } = await execResp.json()

  return { periodId: ctx.periodId, lineId, executionId }
}

/** Updates an ExecutionRecord's amount via the API. */
async function updateExecution(
  request: APIRequestContext,
  ctx: BudgetCtx,
  periodId: string,
  lineId: string,
  executionId: string,
  amount: number,
  operationDate: string,
): Promise<void> {
  const resp = await request.put(
    `/api/budgets/${ctx.budgetId}/periods/${periodId}/budget-lines/${lineId}/executions/${executionId}`,
    {
      headers: ctx.headers,
      data: {
        entryType: 1,
        amount,
        note: 'Snapshot test execution — mutated after cut save',
        operationDate,
        currencyId: GTQ_CURRENCY_ID,
        exchangeRate: null,
        exchangeRateTo: null,
        accountId: null,
        paymentMethodId: null,
      },
    },
  )
  expect(resp.status()).toBe(200)
}

test.describe('CutRecord — Totals Snapshot', () => {
  test('totals displayed right after save remain unchanged after mutating an execution record and reloading', async ({
    request,
  }) => {
    const ctx = await seedBudgetCtx(request, 'cs-snap')
    const accountId = await createBankAccount(request, ctx, {
      alias: 'Snapshot Account',
      isPositive: true,
    })

    const cutDate = '2026-06-15' // within the 2026-01-01..2026-12-31 period seeded by seedBudgetCtx

    const { periodId, lineId, executionId } = await seedExecution(request, ctx, cutDate)

    // Save the cut — this computes and persists all 16 totals server-side,
    // including the execution-derived trio (TotalBudgeted/TotalRegistered/Remaining).
    const putResp = await request.put(TOTALS_TITLE_URL(ctx.budgetId, cutDate), {
      headers: ctx.headers,
      data: {
        exchangeRate: 7.8,
        accounts: [{ bankAccountId: accountId, balance: 1500 }],
      },
    })
    expect(putResp.status()).toBe(200)

    // Reload right after save — this is exactly what CurrentSituationView does
    // on mount/navigation, and what CutTotalsPanel renders via `store.currentRecord`.
    const afterSaveResp = await request.get(TOTALS_TITLE_URL(ctx.budgetId, cutDate), {
      headers: ctx.headers,
    })
    expect(afterSaveResp.status()).toBe(200)
    const afterSave = await afterSaveResp.json()
    expect(afterSave.isDraft).toBe(false)

    const totalsAfterSave = afterSave.totals
    const executionSummaryAfterSave = afterSave.executionSummary

    // Sanity: the execution record contributed a non-zero commitment, so this
    // assertion is meaningful (not vacuously true on all-zero totals).
    expect(executionSummaryAfterSave.totalRegistered).toBeGreaterThan(0)

    // Mutate the execution record in the active period AFTER the cut was saved.
    await updateExecution(request, ctx, periodId, lineId, executionId, 999, cutDate)

    // Reload the cut again — CS-6 snapshot semantics: persisted totals must be frozen.
    const afterMutationResp = await request.get(TOTALS_TITLE_URL(ctx.budgetId, cutDate), {
      headers: ctx.headers,
    })
    expect(afterMutationResp.status()).toBe(200)
    const afterMutation = await afterMutationResp.json()
    expect(afterMutation.isDraft).toBe(false)

    // The displayed totals (CutTotalsPanel props) are unchanged from what was shown
    // right after the save, even though the underlying execution data changed.
    expect(afterMutation.totals).toEqual(totalsAfterSave)
    expect(afterMutation.executionSummary).toEqual(executionSummaryAfterSave)
  })
})
