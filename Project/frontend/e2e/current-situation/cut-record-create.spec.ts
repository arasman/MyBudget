import { test, expect } from '@playwright/test'
import { seedBudgetCtx, createBankAccount, GTQ_CURRENCY_ID } from './helpers'

/**
 * E2E: Create first cut record, verify draft pre-population, save, reload.
 * Specs: CS-1, CS-2, CS-7
 */
test.describe('CutRecord — Create', () => {
  test('draft is pre-populated with active accounts at balance 0', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'cs-create')
    const accId = await createBankAccount(request, ctx, {
      alias: 'Caja GTQ',
      currencyId: GTQ_CURRENCY_ID,
    })

    const today = new Date().toISOString().slice(0, 10)
    const draftResp = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/${today}`,
      { headers: ctx.headers },
    )
    expect(draftResp.status()).toBe(200)
    const draft = await draftResp.json()
    expect(draft.isDraft).toBe(true)

    const acc = (draft.accounts as { bankAccountId: string; balance: number }[]).find(
      (a) => a.bankAccountId === accId,
    )
    expect(acc).toBeDefined()
    expect(acc!.balance).toBe(0)
  })

  test('save cut record and verify data persisted', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'cs-save')
    const accId = await createBankAccount(request, ctx, { alias: 'Banco GTQ' })

    const today = new Date().toISOString().slice(0, 10)

    // Upsert
    const putResp = await request.put(
      `/api/budgets/${ctx.budgetId}/cut-records/${today}`,
      {
        headers: ctx.headers,
        data: {
          exchangeRate: 7.8,
          accounts: [{ bankAccountId: accId, balance: 1500.5 }],
        },
      },
    )
    expect(putResp.status()).toBe(200)

    // Reload
    const getResp = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/${today}`,
      { headers: ctx.headers },
    )
    expect(getResp.status()).toBe(200)
    const record = await getResp.json()
    expect(record.isDraft).toBe(false)
    expect(record.exchangeRate).toBe(7.8)

    const acc = (record.accounts as { bankAccountId: string; balance: number }[]).find(
      (a) => a.bankAccountId === accId,
    )
    expect(acc!.balance).toBe(1500.5)
  })

  test('422 when no active period covers the cut date', async ({ request }) => {
    // Register a fresh user with no cycle/period
    const email = `e2e-cs-noperiod-${Date.now()}@example.com`
    const regResp = await request.post('/api/auth/register', {
      data: {
        email,
        password: 'Password1!',
        firstName: 'E2E',
        lastName: 'NoPeriod',
        preferredLocale: 'en',
      },
    })
    const { accessToken: token } = await regResp.json()
    const headers = { Authorization: `Bearer ${token}` }
    const me = await (await request.get('/api/auth/me', { headers })).json()
    const budgetId: string = me.memberships[0].budgetId

    const putResp = await request.put(
      `/api/budgets/${budgetId}/cut-records/2026-07-01`,
      {
        headers,
        data: { exchangeRate: 7.8, accounts: [] },
      },
    )
    expect(putResp.status()).toBe(422)
  })
})
