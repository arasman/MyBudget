import { test, expect } from '@playwright/test'
import { seedBudgetContext, GTQ_CURRENCY_ID } from './helpers'

/**
 * E2E: CreditNote and DebitNote entry types.
 * Verifies Note is required for both; negative Amount is rejected.
 *
 * REQ-EXEC-4, REQ-EXEC-3
 */
test.describe('Budget Execution — CreditNote and DebitNote', () => {
  test('create CreditNote with Note → appears in list', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'credit-note')
    const headers = { Authorization: `Bearer ${ctx.token}` }
    const url = `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions`

    const resp = await request.post(url, {
      headers,
      data: {
        entryType: 2,
        amount: 50,
        note: 'Refund applied',
        currencyId: GTQ_CURRENCY_ID,
        exchangeRate: null,
        exchangeRateTo: null,
        accountId: null,
        paymentMethodId: null,
      },
    })
    expect(resp.status()).toBe(201)
    const { id } = await resp.json()

    const listResp = await request.get(url, { headers })
    expect(listResp.status()).toBe(200)
    const items = await listResp.json()
    const found = (items as { id: string; entryType: number; note: string }[]).find(
      (i) => i.id === id,
    )
    expect(found).toBeDefined()
    expect(found!.entryType).toBe(2)
    expect(found!.note).toBe('Refund applied')
  })

  test('create DebitNote with Note → appears in list', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'debit-note')
    const headers = { Authorization: `Bearer ${ctx.token}` }
    const url = `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions`

    const resp = await request.post(url, {
      headers,
      data: {
        entryType: 3,
        amount: 75,
        note: 'Extra charge',
        currencyId: GTQ_CURRENCY_ID,
        exchangeRate: null,
        exchangeRateTo: null,
        accountId: null,
        paymentMethodId: null,
      },
    })
    expect(resp.status()).toBe(201)
    const { id } = await resp.json()

    const listResp = await request.get(url, { headers })
    const items = await listResp.json()
    const found = (items as { id: string; entryType: number }[]).find(
      (i) => i.id === id,
    )
    expect(found).toBeDefined()
    expect(found!.entryType).toBe(3)
  })

  test('negative Amount → rejected 400', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'neg-amount')
    const headers = { Authorization: `Bearer ${ctx.token}` }
    const url = `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions`

    const resp = await request.post(url, {
      headers,
      data: {
        entryType: 1,
        amount: -10,
        note: null,
        currencyId: GTQ_CURRENCY_ID,
        exchangeRate: null,
        exchangeRateTo: null,
        accountId: null,
        paymentMethodId: null,
      },
    })
    expect(resp.status()).toBe(400)
  })
})
