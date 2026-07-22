import type { Page } from '@playwright/test'
import { expect } from '@playwright/test'
export { expectToast } from '../helpers/toast'

const PASSWORD = 'Password1!'
const DEFAULT_CURRENCY_ID = '11111111-1111-1111-1111-111111111111'

/**
 * Registers a fresh user via the API and injects tokens into localStorage.
 * The backend automatically creates a budget with Owner membership on registration.
 *
 * @returns the logged-in user's budgetId extracted from /api/auth/me
 */
export async function seedOwnerAndLogin(page: Page, prefix = 'bs'): Promise<{
  email: string
  budgetId: string
  budgetName: string
}> {
  const email = `e2e-${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}@example.com`

  // Register → get tokens
  const regResp = await page.request.post('/api/auth/register', {
    data: {
      email,
      password: PASSWORD,
      firstName: 'E2E',
      lastName: 'Owner',
      preferredLocale: 'en',
    },
  })
  expect(regResp.status()).toBe(201)
  const regBody = await regResp.json()

  // Inject tokens into localStorage (must navigate first)
  await page.goto('/')
  await page.evaluate(
    ({ at, rt }) => {
      localStorage.setItem('accessToken', at)
      localStorage.setItem('refreshToken', rt)
    },
    { at: regBody.accessToken, rt: regBody.refreshToken },
  )

  // Fetch user profile to get budgetId
  const meResp = await page.request.get('/api/auth/me', {
    headers: { Authorization: `Bearer ${regBody.accessToken}` },
  })
  expect(meResp.status()).toBe(200)
  const me = await meResp.json()
  const membership = me.memberships[0]

  return {
    email,
    budgetId: membership.budgetId,
    budgetName: membership.budgetName,
  }
}

/**
 * Registers a second user and invites them as read-only (no operator/admin role).
 * Uses the InviteUser API. Returns the read-only user's login credentials.
 * Note: the invitation API exists but acceptance flow is separate; for role-gating
 * tests, we register a new user and DON'T invite them → they have no memberships,
 * so all role gate checks return false.
 */
export async function seedReadOnlyAndLogin(page: Page): Promise<{
  email: string
}> {
  const email = `e2e-readonly-${Date.now()}@example.com`

  // Register the read-only user (no budget membership — just a registered account)
  const regResp = await page.request.post('/api/auth/register', {
    data: {
      email,
      password: PASSWORD,
      firstName: 'E2E',
      lastName: 'ReadOnly',
      preferredLocale: 'en',
    },
  })
  expect(regResp.status()).toBe(201)
  const regBody = await regResp.json()

  await page.goto('/')
  await page.evaluate(
    ({ at, rt }) => {
      localStorage.setItem('accessToken', at)
      localStorage.setItem('refreshToken', rt)
    },
    { at: regBody.accessToken, rt: regBody.refreshToken },
  )

  return { email }
}

/** Login using the UI form — for cases where tokens aren't injected directly. */
export async function loginViaUi(page: Page, email: string): Promise<void> {
  await page.goto('/login')
  await page.getByPlaceholder('you@example.com').fill(email)
  await page.getByPlaceholder('Password').fill(PASSWORD)
  await page.getByRole('button', { name: 'Sign In' }).click()
  await expect(page).toHaveURL(/\/(budgets\/[^/]+\/cycles)?$/, { timeout: 10_000 })
}


/**
 * Creates a cycle via API and immediately soft-deletes it.
 * Returns the deleted cycle's id.
 */
export async function seedDeletedCycle(
  page: Page,
  budgetId: string,
  token: string,
): Promise<string> {
  const headers = { Authorization: `Bearer ${token}` }

  const createResp = await page.request.post(`/api/budgets/${budgetId}/cycles`, {
    headers,
    data: {
      name: `Deleted Cycle ${Date.now()}`,
      startDate: '2023-01-01',
      endDate: '2023-12-31',
      defaultCurrencyId: DEFAULT_CURRENCY_ID,
    },
  })
  expect(createResp.status()).toBe(201)
  const { id } = await createResp.json()

  const deleteResp = await page.request.delete(`/api/budgets/${budgetId}/cycles/${id}`, {
    headers,
  })
  expect(deleteResp.status()).toBe(204)

  return id as string
}

/**
 * Creates a period within a cycle via API and immediately soft-deletes it.
 * Returns the deleted period's id.
 */
export async function seedDeletedPeriod(
  page: Page,
  budgetId: string,
  cycleId: string,
  token: string,
): Promise<string> {
  const headers = { Authorization: `Bearer ${token}` }

  const createResp = await page.request.post(
    `/api/budgets/${budgetId}/cycles/${cycleId}/periods`,
    {
      headers,
      data: {
        name: `Deleted Period ${Date.now()}`,
        periodNumber: 99,
        startDate: '2024-11-01',
        endDate: '2024-11-30',
      },
    },
  )
  expect(createResp.status()).toBe(201)
  const { id } = await createResp.json()

  const deleteResp = await page.request.delete(
    `/api/budgets/${budgetId}/cycles/${cycleId}/periods/${id}`,
    { headers },
  )
  expect(deleteResp.status()).toBe(204)

  return id as string
}

/**
 * Creates a category group via API and immediately soft-deletes it.
 * Returns the deleted category group's id.
 */
export async function seedDeletedCategoryGroup(
  page: Page,
  budgetId: string,
  token: string,
): Promise<string> {
  const headers = { Authorization: `Bearer ${token}` }

  const createResp = await page.request.post(`/api/budgets/${budgetId}/category-groups`, {
    headers,
    data: { name: `Deleted Group ${Date.now()}`, displayOrder: 99 },
  })
  expect(createResp.status()).toBe(201)
  const { id } = await createResp.json()

  const deleteResp = await page.request.delete(
    `/api/budgets/${budgetId}/category-groups/${id}`,
    { headers },
  )
  expect(deleteResp.status()).toBe(204)

  return id as string
}

/**
 * Creates a category within a group via API and immediately soft-deletes it.
 * Returns the deleted category's id.
 */
export async function seedDeletedCategory(
  page: Page,
  budgetId: string,
  groupId: string,
  token: string,
): Promise<string> {
  const headers = { Authorization: `Bearer ${token}` }

  const createResp = await page.request.post(
    `/api/budgets/${budgetId}/category-groups/${groupId}/categories`,
    {
      headers,
      data: { name: `Deleted Category ${Date.now()}`, displayOrder: 99 },
    },
  )
  expect(createResp.status()).toBe(201)
  const { id } = await createResp.json()

  const deleteResp = await page.request.delete(
    `/api/budgets/${budgetId}/category-groups/${groupId}/categories/${id}`,
    { headers },
  )
  expect(deleteResp.status()).toBe(204)

  return id as string
}

/**
 * Creates a budget line within a period via API and immediately soft-deletes it.
 * Internally creates a temporary category group to satisfy the line's required FK.
 * Returns the deleted budget line's id.
 */
export async function seedDeletedBudgetLine(
  page: Page,
  budgetId: string,
  periodId: string,
  token: string,
): Promise<string> {
  const headers = { Authorization: `Bearer ${token}` }

  // Create a transient category group (required FK for budget lines)
  const groupResp = await page.request.post(`/api/budgets/${budgetId}/category-groups`, {
    headers,
    data: { name: `Seed Group ${Date.now()}`, displayOrder: 99 },
  })
  expect(groupResp.status()).toBe(201)
  const { id: categoryGroupId } = await groupResp.json()

  const createResp = await page.request.post(
    `/api/budgets/${budgetId}/lines`,
    {
      headers,
      data: {
        name: `Deleted Line ${Date.now()}`,
        lineType: 'Expense',
        categoryGroupId,
        categoryId: null,
        startDate: '2020-01-01',
        endDate: null,
        initialAmount: 100,
        currencyId: DEFAULT_CURRENCY_ID,
      },
    },
  )
  expect(createResp.status()).toBe(201)
  const { id } = await createResp.json()

  const deleteResp = await page.request.delete(
    `/api/budgets/${budgetId}/lines/${id}`,
    { headers },
  )
  expect(deleteResp.status()).toBe(204)

  return id as string
}
