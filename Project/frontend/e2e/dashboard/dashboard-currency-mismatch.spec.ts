import type { APIRequestContext } from '@playwright/test'
import { test, expect } from '@playwright/test'
import { GTQ_CURRENCY_ID, USD_CURRENCY_ID, PASSWORD, loginWithToken, goToDashboard } from './helpers'

/**
 * E2E: DASH-12 cross-cycle currency-mismatch guard — comparing BudgetLines
 * across a GTQ cycle and a USD cycle must warn the user and MUST NOT render
 * one blended-currency chart.
 * Spec: DASH-12.
 */

interface MismatchFixture {
  budgetId: string
  lineId: string
  period1Id: string
  period2Id: string
  accessToken: string
  headers: Record<string, string>
}

/** Seeds one budget with 2 Cycles of different DefaultCurrencyId, each with 1 Period, sharing one BudgetLine. */
async function seedMismatchFixture(request: APIRequestContext, prefix: string): Promise<MismatchFixture> {
  const email = `e2e-${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}@example.com`
  const regResp = await request.post('/api/auth/register', {
    data: { email, password: PASSWORD, firstName: 'E2E', lastName: 'Mismatch', preferredLocale: 'en' },
  })
  expect(regResp.status()).toBe(201)
  const { accessToken } = await regResp.json()
  const headers = { Authorization: `Bearer ${accessToken}` }

  const meResp = await request.get('/api/auth/me', { headers })
  const me = await meResp.json()
  const budgetId: string = me.memberships[0].budgetId

  // Cycle 1 — GTQ default currency.
  const cycle1Resp = await request.post(`/api/budgets/${budgetId}/cycles`, {
    headers,
    data: {
      name: 'GTQ Cycle',
      startDate: '2026-01-01',
      endDate: '2026-06-30',
      defaultCurrencyId: GTQ_CURRENCY_ID,
      alternateCurrencyId: USD_CURRENCY_ID,
      exchangeRate: 7.8,
    },
  })
  expect(cycle1Resp.status()).toBe(201)
  const { id: cycle1Id } = await cycle1Resp.json()

  const period1Resp = await request.post(`/api/budgets/${budgetId}/cycles/${cycle1Id}/periods`, {
    headers,
    data: { name: 'GTQ Period', periodNumber: 1, startDate: '2026-01-01', endDate: '2026-06-30' },
  })
  expect(period1Resp.status()).toBe(201)
  const { id: period1Id } = await period1Resp.json()

  // Cycle 2 — USD default currency (the mismatch).
  const cycle2Resp = await request.post(`/api/budgets/${budgetId}/cycles`, {
    headers,
    data: {
      name: 'USD Cycle',
      startDate: '2026-07-01',
      endDate: '2026-12-31',
      defaultCurrencyId: USD_CURRENCY_ID,
      alternateCurrencyId: GTQ_CURRENCY_ID,
      exchangeRate: 7.8,
    },
  })
  expect(cycle2Resp.status()).toBe(201)
  const { id: cycle2Id } = await cycle2Resp.json()

  const period2Resp = await request.post(`/api/budgets/${budgetId}/cycles/${cycle2Id}/periods`, {
    headers,
    data: { name: 'USD Period', periodNumber: 1, startDate: '2026-07-01', endDate: '2026-12-31' },
  })
  expect(period2Resp.status()).toBe(201)
  const { id: period2Id } = await period2Resp.json()

  // One BudgetLine, budget-scoped — same identity compared cross-cycle (DASH-4).
  const groupResp = await request.post(`/api/budgets/${budgetId}/category-groups`, {
    headers,
    data: { name: 'Mismatch Group', displayOrder: 1 },
  })
  expect(groupResp.status()).toBe(201)
  const { id: groupId } = await groupResp.json()

  const lineResp = await request.post(`/api/budgets/${budgetId}/lines`, {
    headers,
    data: {
      name: 'Cross-Cycle Line',
      lineType: 'Expense',
      categoryGroupId: groupId,
      startDate: '2026-01-01',
      endDate: null,
      initialAmount: 300,
      currencyId: GTQ_CURRENCY_ID,
    },
  })
  expect(lineResp.status()).toBe(201)
  const { id: lineId } = await lineResp.json()

  return { budgetId, lineId, period1Id, period2Id, accessToken, headers }
}

test.describe('Dashboard — cross-cycle currency mismatch (DASH-12)', () => {
  test('comparing a BudgetLine across a GTQ cycle and a USD cycle warns instead of rendering a blended chart', async ({
    page,
    request,
  }) => {
    const fixture = await seedMismatchFixture(request, 'dash-mismatch')

    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToDashboard(page, fixture.budgetId)

    const lineSection = page.locator('section', { hasText: 'Budget Line Behavior' })
    await expect(lineSection).toBeVisible({ timeout: 10_000 })

    await lineSection.getByLabel('Cross-Cycle Line').check()
    await lineSection.getByRole('button', { name: 'Cross-cycle' }).click()
    await lineSection.getByLabel('GTQ Cycle').check()
    await lineSection.getByLabel('USD Cycle').check()

    // DASH-12: warning renders, chart does not — never one blended-currency chart.
    await expect(lineSection.getByRole('alert')).toBeVisible({ timeout: 10_000 })
    await expect(lineSection.getByText('Currency mismatch')).toBeVisible()
    await expect(lineSection.locator('canvas')).toHaveCount(0)
  })
})
