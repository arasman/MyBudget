import { test, expect } from '@playwright/test'
import { seedBudgetContext, createExecution, GTQ_CURRENCY_ID } from './helpers'

/**
 * E2E: Create ExecutionRecord — Expense entry happy path.
 * Verifies that a new Expense record is created and appears in the list.
 *
 * REQ-EXEC-CREATE-1, REQ-EXEC-LIST-1
 */
test.describe('Budget Execution — Create Expense', () => {
  test('create Expense entry → appears in list', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'create-exec')

    // Create an Expense ExecutionRecord via API
    const execId = await createExecution(request, ctx, {
      entryType: 1,
      amount: 250,
      note: null,
      currencyId: GTQ_CURRENCY_ID,
    })
    expect(execId).toBeTruthy()

    // Verify it appears in the list
    const listResp = await request.get(
      `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions`,
      { headers: { Authorization: `Bearer ${ctx.token}` } },
    )
    expect(listResp.status()).toBe(200)
    const items = await listResp.json()
    const found = (items as { id: string; amount: number }[]).find(
      (i) => i.id === execId,
    )
    expect(found).toBeDefined()
    expect(found!.amount).toBe(250)
  })
})
