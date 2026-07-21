import type { APIRequestContext, Page } from '@playwright/test'
import { expect } from '@playwright/test'
import { loginWithToken as _loginWithToken } from '../helpers/auth'

export const PASSWORD = 'Password1!'
export const GTQ_CURRENCY_ID = '11111111-1111-1111-1111-111111111111'
export const USD_CURRENCY_ID = '22222222-2222-2222-2222-222222222222'
export const EXCHANGE_RATE = 7.5

export interface MatrixFixture {
  budgetId: string
  cycleId: string
  periodIds: string[]   // 4 periods (Jan–Apr) — enough to test prev/next navigation
  groupIds: string[]    // 2 groups
  categoryIds: string[] // 2 categories (one per group)
  lineIds: string[]     // budget-line IDs (per period × per category — flat array)
  accessToken: string
}

/**
 * Seeds everything needed to render a populated BudgetMatrixView.
 * Uses the API request fixture (no browser UI) for speed.
 * Returns all IDs required by the matrix E2E specs.
 */
export async function seedBudgetMatrixFixture(
  request: APIRequestContext,
  emailPrefix: string,
): Promise<MatrixFixture> {
  const email = `e2e-${emailPrefix}-${Date.now()}@example.com`

  // ── 1. Register user ──────────────────────────────────────────────────────
  const regResp = await request.post('/api/auth/register', {
    data: {
      email,
      password: PASSWORD,
      firstName: 'E2E',
      lastName: 'MatrixUser',
      preferredLocale: 'en',
    },
  })
  expect(regResp.status()).toBe(201)
  const regBody = await regResp.json()
  const accessToken: string = regBody.accessToken

  const headers = { Authorization: `Bearer ${accessToken}` }

  // Resolve budgetId from /me
  const meResp = await request.get('/api/auth/me', { headers })
  expect(meResp.status()).toBe(200)
  const me = await meResp.json()
  const budgetId: string = me.memberships[0].budgetId

  // ── 2. Create cycle (GTQ default, USD alternate, exchangeRate 7.5) ────────
  const cycleResp = await request.post(`/api/budgets/${budgetId}/cycles`, {
    headers,
    data: {
      name: 'E2E Matrix Cycle',
      startDate: '2025-01-01',
      endDate: '2025-12-31',
      defaultCurrencyId: GTQ_CURRENCY_ID,
      alternateCurrencyId: USD_CURRENCY_ID,
      exchangeRate: EXCHANGE_RATE,
    },
  })
  expect(cycleResp.status()).toBe(201)
  const { id: cycleId } = await cycleResp.json()

  // ── 3. Create 4 periods (Jan–Apr) ─────────────────────────────────────────
  const periodDefs = [
    { name: 'January',  periodNumber: 1, startDate: '2025-01-01', endDate: '2025-01-31' },
    { name: 'February', periodNumber: 2, startDate: '2025-02-01', endDate: '2025-02-28' },
    { name: 'March',    periodNumber: 3, startDate: '2025-03-01', endDate: '2025-03-31' },
    { name: 'April',    periodNumber: 4, startDate: '2025-04-01', endDate: '2025-04-30' },
  ]

  const periodIds: string[] = []
  for (const def of periodDefs) {
    const resp = await request.post(`/api/budgets/${budgetId}/cycles/${cycleId}/periods`, {
      headers,
      data: def,
    })
    expect(resp.status()).toBe(201)
    const { id } = await resp.json()
    periodIds.push(id)
  }

  // ── 4. Set cycle as active ────────────────────────────────────────────────
  // Some backends use a PATCH /cycles/{id}/activate or similar endpoint.
  // Attempt to set active; ignore 404/405 — the cycle is usable regardless.
  await request.patch(`/api/budgets/${budgetId}/cycles/${cycleId}/activate`, {
    headers,
  })

  // ── 5. Create 2 CategoryGroups ────────────────────────────────────────────
  const groupIds: string[] = []
  const groupDefs = [
    { name: 'Housing', displayOrder: 1 },
    { name: 'Food',    displayOrder: 2 },
  ]
  for (const def of groupDefs) {
    const resp = await request.post(`/api/budgets/${budgetId}/category-groups`, {
      headers,
      data: def,
    })
    expect(resp.status()).toBe(201)
    const { id } = await resp.json()
    groupIds.push(id)
  }

  // ── 6. Create 1 Category per group ────────────────────────────────────────
  const categoryIds: string[] = []
  const categoryDefs = [
    { name: 'Rent',     categoryGroupId: groupIds[0], displayOrder: 1 },
    { name: 'Groceries', categoryGroupId: groupIds[1], displayOrder: 1 },
  ]
  for (const def of categoryDefs) {
    const resp = await request.post(
      `/api/budgets/${budgetId}/category-groups/${def.categoryGroupId}/categories`,
      { headers, data: { name: def.name, displayOrder: def.displayOrder } },
    )
    expect(resp.status()).toBe(201)
    const { id } = await resp.json()
    categoryIds.push(id)
  }

  // ── 7. Create 1 BudgetLine per category per period ────────────────────────
  const lineIds: string[] = []
  const lineDefs = [
    { name: 'Rent Payment',    lineType: 'Expense', categoryGroupId: groupIds[0], categoryId: categoryIds[0], budgetedAmount: 5000 },
    { name: 'Weekly Groceries', lineType: 'Expense', categoryGroupId: groupIds[1], categoryId: categoryIds[1], budgetedAmount: 2000 },
  ]

  for (const periodId of periodIds) {
    for (const def of lineDefs) {
      const resp = await request.post(`/api/budgets/${budgetId}/periods/${periodId}/lines`, {
        headers,
        data: {
          name: def.name,
          lineType: def.lineType,
          isRecurring: false,
          categoryGroupId: def.categoryGroupId,
          categoryId: def.categoryId,
          budgetedAmount: def.budgetedAmount,
          currencyId: GTQ_CURRENCY_ID,
        },
      })
      expect(resp.status()).toBe(201)
      const { id } = await resp.json()
      lineIds.push(id)
    }
  }

  return { budgetId, cycleId, periodIds, groupIds, categoryIds, lineIds, accessToken }
}

/**
 * Injects the access token and active budget into browser localStorage so
 * the Vue app considers the user authenticated without a UI login.
 *
 * Delegates to the shared e2e/helpers/auth.ts implementation.
 * Keeps the original positional signature so existing callers need no changes.
 */
export async function loginWithToken(
  page: Page,
  accessToken: string,
  budgetId: string,
): Promise<void> {
  return _loginWithToken(page, { accessToken, activeBudgetId: budgetId })
}

/**
 * Navigates to the BudgetMatrixView for a given budget + cycle and waits for
 * the network to settle.
 */
export async function goToMatrix(
  page: Page,
  budgetId: string,
  cycleId: string,
): Promise<void> {
  await page.goto(`/budgets/${budgetId}/cycles/${cycleId}/matrix`)
  await page.waitForLoadState('networkidle')
}

/**
 * Closes a period via the API.
 */
export async function closePeriodApi(
  request: APIRequestContext,
  budgetId: string,
  cycleId: string,
  periodId: string,
  token: string,
): Promise<void> {
  const url = `/api/budgets/${budgetId}/cycles/${cycleId}/periods/${periodId}/status`
  await request.patch(url, {
    headers: { Authorization: `Bearer ${token}` },
    data: { isClosed: true },
  })
}

/**
 * Soft-deletes a category group via the API.
 */
export async function deleteGroupApi(
  request: APIRequestContext,
  budgetId: string,
  groupId: string,
  token: string,
): Promise<void> {
  const url = `/api/budgets/${budgetId}/category-groups/${groupId}`
  await request.delete(url, {
    headers: { Authorization: `Bearer ${token}` },
  })
}

/**
 * Creates an ExecutionRecord via the API. Returns the new record's ID.
 */
export async function createExecutionApi(
  request: APIRequestContext,
  budgetId: string,
  periodId: string,
  lineId: string,
  token: string,
  options: {
    entryType?: number
    amount?: number
    note?: string | null
    operationDate?: string
  } = {},
): Promise<string> {
  const { entryType = 1, amount = 100, note = 'Test execution note', operationDate = '2025-01-15' } = options
  const url = `/api/budgets/${budgetId}/periods/${periodId}/budget-lines/${lineId}/executions`
  const resp = await request.post(url, {
    headers: { Authorization: `Bearer ${token}` },
    data: {
      entryType,
      amount,
      note,
      operationDate,
      currencyId: GTQ_CURRENCY_ID,
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

/**
 * Registers a second user with no budget membership (simulates budget:read / non-member).
 * Returns the access token.
 */
export async function seedNonMemberUser(
  request: APIRequestContext,
  emailPrefix: string,
): Promise<{ accessToken: string }> {
  const email = `e2e-${emailPrefix}-${Date.now()}@example.com`
  const regResp = await request.post('/api/auth/register', {
    data: {
      email,
      password: PASSWORD,
      firstName: 'NonMember',
      lastName: 'User',
      preferredLocale: 'en',
    },
  })
  expect(regResp.status()).toBe(201)
  const { accessToken } = await regResp.json()
  return { accessToken }
}
