import type { APIRequestContext } from '@playwright/test'
import { expect } from '@playwright/test'

export const PASSWORD = 'Password1!'
export const GTQ_CURRENCY_ID = '11111111-1111-1111-1111-111111111111'
export const USD_CURRENCY_ID = '22222222-2222-2222-2222-222222222222'

export interface BudgetCtx {
  budgetId: string
  cycleId: string
  periodId: string
  token: string
  headers: Record<string, string>
}

/**
 * Registers a fresh user with a budget, cycle, and one active period.
 * Returns all IDs + auth token.
 */
export async function seedBudgetCtx(
  request: APIRequestContext,
  prefix = 'cs',
): Promise<BudgetCtx> {
  const email = `e2e-${prefix}-${Date.now()}@example.com`

  const regResp = await request.post('/api/auth/register', {
    data: {
      email,
      password: PASSWORD,
      firstName: 'E2E',
      lastName: 'CS',
      preferredLocale: 'en',
    },
  })
  expect(regResp.status()).toBe(201)
  const { accessToken: token } = await regResp.json()

  const headers = { Authorization: `Bearer ${token}` }

  const meResp = await request.get('/api/auth/me', { headers })
  const me = await meResp.json()
  const budgetId: string = me.memberships[0].budgetId

  // Create cycle with primary GTQ + alternate USD
  const cycleResp = await request.post(`/api/budgets/${budgetId}/cycles`, {
    headers,
    data: {
      name: 'E2E CS Cycle',
      startDate: '2026-01-01',
      endDate: '2026-12-31',
      defaultCurrencyId: GTQ_CURRENCY_ID,
      alternateCurrencyId: USD_CURRENCY_ID,
      exchangeRate: 7.8,
    },
  })
  expect(cycleResp.status()).toBe(201)
  const { id: cycleId } = await cycleResp.json()

  // Create period covering today's date range for cut records
  const periodResp = await request.post(`/api/budgets/${budgetId}/cycles/${cycleId}/periods`, {
    headers,
    data: {
      name: 'E2E Period',
      periodNumber: 1,
      startDate: '2026-01-01',
      endDate: '2026-12-31',
    },
  })
  expect(periodResp.status()).toBe(201)
  const { id: periodId } = await periodResp.json()

  return { budgetId, cycleId, periodId, token, headers }
}

/** Creates a bank account and returns its id. */
export async function createBankAccount(
  request: APIRequestContext,
  ctx: BudgetCtx,
  options: {
    alias?: string
    currencyId?: string
    isPositive?: boolean
    displayOrder?: number
  } = {},
): Promise<string> {
  const {
    alias = 'Caja GTQ',
    currencyId = GTQ_CURRENCY_ID,
    isPositive = true,
    displayOrder = 0,
  } = options

  const resp = await request.post(`/api/budgets/${ctx.budgetId}/bank-accounts`, {
    headers: ctx.headers,
    data: { alias, currencyId, isPositive, displayOrder },
  })
  expect(resp.status()).toBe(201)
  const { id } = await resp.json()
  return id as string
}

/** Creates a cut record for the given date. */
export async function upsertCutRecord(
  request: APIRequestContext,
  ctx: BudgetCtx,
  date: string,
  accounts: { bankAccountId: string; balance: number }[],
  exchangeRate = 7.8,
): Promise<void> {
  const resp = await request.put(
    `/api/budgets/${ctx.budgetId}/cut-records/${date}`,
    {
      headers: ctx.headers,
      data: { exchangeRate, accounts },
    },
  )
  expect(resp.status()).toBe(200)
}
