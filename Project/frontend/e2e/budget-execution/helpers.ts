import type { APIRequestContext } from '@playwright/test'
import { expect } from '@playwright/test'

export const PASSWORD = 'Password1!'
export const GTQ_CURRENCY_ID = '11111111-1111-1111-1111-111111111111'
export const USD_CURRENCY_ID = '22222222-2222-2222-2222-222222222222'

export interface BudgetContext {
  budgetId: string
  cycleId: string
  periodId: string
  lineId: string
  token: string
}

/**
 * Registers a fresh user, creates cycle/period/category-group/budget-line, and
 * returns all IDs + the access token.
 */
export async function seedBudgetContext(
  request: APIRequestContext,
  prefix = 'exec',
): Promise<BudgetContext> {
  const email = `e2e-${prefix}-${Date.now()}@example.com`

  // Register user
  const regResp = await request.post('/api/auth/register', {
    data: {
      email,
      password: PASSWORD,
      firstName: 'E2E',
      lastName: 'Operator',
      preferredLocale: 'en',
    },
  })
  expect(regResp.status()).toBe(201)
  const regBody = await regResp.json()
  const token: string = regBody.accessToken

  const headers = { Authorization: `Bearer ${token}` }

  // Get budgetId from /me
  const meResp = await request.get('/api/auth/me', { headers })
  const me = await meResp.json()
  const budgetId: string = me.memberships[0].budgetId

  // Create cycle
  const cycleResp = await request.post(`/api/budgets/${budgetId}/cycles`, {
    headers,
    data: {
      name: 'E2E Cycle',
      startDate: '2025-01-01',
      endDate: '2025-12-31',
      defaultCurrencyId: GTQ_CURRENCY_ID,
    },
  })
  expect(cycleResp.status()).toBe(201)
  const { id: cycleId } = await cycleResp.json()

  // Create period
  const periodResp = await request.post(`/api/budgets/${budgetId}/cycles/${cycleId}/periods`, {
    headers,
    data: {
      name: 'January',
      periodNumber: 1,
      startDate: '2025-01-01',
      endDate: '2025-01-31',
    },
  })
  expect(periodResp.status()).toBe(201)
  const { id: periodId } = await periodResp.json()

  // Create category group
  const groupResp = await request.post(`/api/budgets/${budgetId}/category-groups`, {
    headers,
    data: { name: 'Housing', displayOrder: 1 },
  })
  expect(groupResp.status()).toBe(201)
  const { id: groupId } = await groupResp.json()

  // Create budget line (budget-level, no periodId)
  const lineResp = await request.post(`/api/budgets/${budgetId}/lines`, {
    headers,
    data: {
      name: 'Rent',
      lineType: 'Expense',
      categoryGroupId: groupId,
      startDate: '2024-01-01',
      endDate: null,
      initialAmount: 5000,
      currencyId: GTQ_CURRENCY_ID,
    },
  })
  expect(lineResp.status()).toBe(201)
  const { id: lineId } = await lineResp.json()

  return { budgetId, cycleId, periodId, lineId, token }
}

/** Creates an ExecutionRecord via the API. Returns the new record's ID. */
export async function createExecution(
  request: APIRequestContext,
  ctx: BudgetContext,
  options: {
    entryType?: number
    amount?: number
    note?: string | null
    currencyId?: string
    operationDate?: string
  } = {},
): Promise<string> {
  const {
    entryType = 1,
    amount = 100,
    note = 'Test execution note',
    currencyId = GTQ_CURRENCY_ID,
    operationDate = '2025-01-15',
  } = options

  const url = `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions`
  const resp = await request.post(url, {
    headers: { Authorization: `Bearer ${ctx.token}` },
    data: {
      entryType,
      amount,
      note,
      operationDate,
      currencyId,
      exchangeRate: null,
      exchangeRateTo: null,
      accountId: null,
      paymentMethodId: null,
    },
  })
  expect(resp.status()).toBe(201)
  const body = await resp.json()
  return body.id as string
}

/** Soft-deletes an ExecutionRecord via the API. */
export async function deleteExecution(
  request: APIRequestContext,
  ctx: BudgetContext,
  executionId: string,
): Promise<void> {
  const url = `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions/${executionId}`
  const resp = await request.delete(url, {
    headers: { Authorization: `Bearer ${ctx.token}` },
  })
  expect(resp.status()).toBe(204)
}

/** Closes the current period via the API. */
export async function closePeriod(
  request: APIRequestContext,
  ctx: BudgetContext,
): Promise<void> {
  const url = `/api/budgets/${ctx.budgetId}/cycles/${ctx.cycleId}/periods/${ctx.periodId}/status`
  await request.patch(url, {
    headers: { Authorization: `Bearer ${ctx.token}` },
    data: { isClosed: true },
  })
}
