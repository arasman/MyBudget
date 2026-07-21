import { test, expect } from '@playwright/test'
import { seedBudgetContext, createExecution, closePeriod, GTQ_CURRENCY_ID } from './helpers'

/**
 * E2E: Period closed guard — write operations blocked when period is closed.
 *
 * REQ-EXEC-CLOSED-1
 */
test.describe('Budget Execution — Period Closed Guard', () => {
  test('create on closed period → 409 PERIOD_CLOSED', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'closed-create')
    const headers = { Authorization: `Bearer ${ctx.token}` }

    await closePeriod(request, ctx)

    const resp = await request.post(
      `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions`,
      {
        headers,
        data: {
          entryType: 1,
          amount: 100,
          note: 'Test execution note',
          operationDate: '2025-01-15',
          currencyId: GTQ_CURRENCY_ID,
          exchangeRate: null,
          exchangeRateTo: null,
          accountId: null,
          paymentMethodId: null,
        },
      },
    )
    expect(resp.status()).toBe(409)
    const body = await resp.json()
    expect(body.error).toBe('PERIOD_CLOSED')
  })

  test('update on closed period → 409 PERIOD_CLOSED', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'closed-update')
    const headers = { Authorization: `Bearer ${ctx.token}` }

    // Create execution while open
    const execId = await createExecution(request, ctx, { amount: 100 })

    // Close the period
    await closePeriod(request, ctx)

    // Attempt update
    const updateResp = await request.put(
      `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions/${execId}`,
      {
        headers,
        data: {
          entryType: 1,
          amount: 200,
          note: 'Test execution note',
          operationDate: '2025-01-15',
          currencyId: GTQ_CURRENCY_ID,
          exchangeRate: null,
          exchangeRateTo: null,
          accountId: null,
          paymentMethodId: null,
        },
      },
    )
    expect(updateResp.status()).toBe(409)
    const body = await updateResp.json()
    expect(body.error).toBe('PERIOD_CLOSED')
  })
})
