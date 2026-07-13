import { test, expect } from '@playwright/test'
import { seedBudgetContext, createExecution, GTQ_CURRENCY_ID, PASSWORD } from './helpers'

/**
 * E2E: RBAC role enforcement for BudgetExecution endpoints.
 * budget:read cannot create/update/delete; budget:operator can.
 *
 * REQ-EXEC-CREATE-1, REQ-EXEC-UPDATE-1, REQ-EXEC-DELETE-1
 */
test.describe('Budget Execution — RBAC', () => {
  test('budget:read cannot create an execution record', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'rbac-read')

    // Register a second user (no budget membership → no operator role)
    const readEmail = `e2e-read-only-${Date.now()}@example.com`
    const regResp = await request.post('/api/auth/register', {
      data: {
        email: readEmail,
        password: PASSWORD,
        firstName: 'ReadOnly',
        lastName: 'User',
        preferredLocale: 'en',
      },
    })
    expect(regResp.status()).toBe(201)
    const { accessToken: readToken } = await regResp.json()
    const readHeaders = { Authorization: `Bearer ${readToken}` }

    // Attempt to create an execution record as a user with no membership
    const resp = await request.post(
      `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions`,
      {
        headers: readHeaders,
        data: {
          entryType: 1,
          amount: 100,
          note: null,
          currencyId: GTQ_CURRENCY_ID,
          exchangeRate: null,
          exchangeRateTo: null,
          accountId: null,
          paymentMethodId: null,
        },
      },
    )
    // No membership → 403 or 404 (resource guard converts 403 to 404)
    expect([403, 404]).toContain(resp.status())
  })

  test('budget:operator can create an execution record', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'rbac-operator')
    const headers = { Authorization: `Bearer ${ctx.token}` }
    const url = `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions`

    const resp = await request.post(url, {
      headers,
      data: {
        entryType: 1,
        amount: 100,
        note: null,
        currencyId: GTQ_CURRENCY_ID,
        exchangeRate: null,
        exchangeRateTo: null,
        accountId: null,
        paymentMethodId: null,
      },
    })
    expect(resp.status()).toBe(201)
  })

  test('budget:read cannot delete an execution record', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'rbac-del')
    const execId = await createExecution(request, ctx, { amount: 100 })

    // Register a read-only user (no membership)
    const readEmail = `e2e-read-del-${Date.now()}@example.com`
    const regResp = await request.post('/api/auth/register', {
      data: {
        email: readEmail,
        password: PASSWORD,
        firstName: 'ReadOnly',
        lastName: 'User',
        preferredLocale: 'en',
      },
    })
    const { accessToken: readToken } = await regResp.json()
    const readHeaders = { Authorization: `Bearer ${readToken}` }

    const deleteResp = await request.delete(
      `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions/${execId}`,
      { headers: readHeaders },
    )
    expect([403, 404]).toContain(deleteResp.status())
  })
})
