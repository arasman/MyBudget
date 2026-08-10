import type { Page } from '@playwright/test'
import { expect } from '@playwright/test'

export { expectToast } from '../helpers/toast'

const PASSWORD = 'Password1!'

/**
 * Registers a fresh user via the API and injects tokens into localStorage.
 * The backend auto-creates a budget with Owner membership on registration.
 * Kept self-contained (not imported from ../budget-structure/helpers) so the
 * screenshots suite has no coupling to other flow suites' internal helpers.
 */
export async function seedOwnerAndLogin(
  page: Page,
  prefix = 'slide',
): Promise<{ email: string; password: string; budgetId: string; accessToken: string }> {
  const email = `e2e-${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 7)}@example.com`

  const regResp = await page.request.post('/api/auth/register', {
    data: {
      email,
      password: PASSWORD,
      firstName: 'E2E',
      lastName: 'Slides',
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

  const meResp = await page.request.get('/api/auth/me', {
    headers: { Authorization: `Bearer ${regBody.accessToken}` },
  })
  expect(meResp.status()).toBe(200)
  const me = await meResp.json()

  return { email, password: PASSWORD, budgetId: me.memberships[0].budgetId, accessToken: regBody.accessToken }
}

/**
 * Waits for all currently-visible toasts to auto-dismiss (each has its own
 * 3s timer — see toast.store.ts DEFAULT_AUTO_DISMISS_MS). Toasts stack
 * (position: fixed) and don't affect document scrollHeight, so a leftover
 * toast from a prior step can push a later one past what a fullPage
 * screenshot captures — call this after each toast-producing shoot() within
 * an SPA session (no full page navigation) to keep captures single-toast-clean.
 * Clicking each toast's Close button instead races the leave transition
 * (TransitionGroup re-keys mid-click) — waiting out the fixed timer is slower
 * but flake-free.
 */
export async function dismissToasts(page: Page): Promise<void> {
  await expect(page.getByRole('alert')).toHaveCount(0, { timeout: 5_000 })
}

const DEFAULT_CURRENCY_ID = '11111111-1111-1111-1111-111111111111'

async function authHeaders(page: Page): Promise<{ Authorization: string }> {
  const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')
  return { Authorization: `Bearer ${token}` }
}

/** Creates a cycle via API — for flows that need to start deeper than the cycles list. */
export async function createCycleViaApi(
  page: Page,
  budgetId: string,
  name: string,
): Promise<string> {
  const resp = await page.request.post(`/api/budgets/${budgetId}/cycles`, {
    headers: await authHeaders(page),
    data: { name, startDate: '2024-01-01', endDate: '2024-12-31', defaultCurrencyId: DEFAULT_CURRENCY_ID },
  })
  expect(resp.status()).toBe(201)
  const { id } = await resp.json()
  return id as string
}

/** Creates a period within a cycle via API. */
export async function createPeriodViaApi(
  page: Page,
  budgetId: string,
  cycleId: string,
  name: string,
  periodNumber: number,
): Promise<string> {
  const resp = await page.request.post(`/api/budgets/${budgetId}/cycles/${cycleId}/periods`, {
    headers: await authHeaders(page),
    data: { name, periodNumber, startDate: '2024-01-01', endDate: '2024-01-31' },
  })
  expect(resp.status()).toBe(201)
  const { id } = await resp.json()
  return id as string
}

/** Creates a bank account via API. */
export async function createBankAccountViaApi(
  page: Page,
  budgetId: string,
  alias: string,
  isPositive = true,
): Promise<string> {
  const resp = await page.request.post(`/api/budgets/${budgetId}/bank-accounts`, {
    headers: await authHeaders(page),
    data: { alias, currencyId: DEFAULT_CURRENCY_ID, isPositive, displayOrder: 0 },
  })
  expect(resp.status()).toBe(201)
  const { id } = await resp.json()
  return id as string
}

/** Creates a category group via API — required FK for budget lines. */
export async function createCategoryGroupViaApi(
  page: Page,
  budgetId: string,
  name: string,
): Promise<string> {
  const resp = await page.request.post(`/api/budgets/${budgetId}/category-groups`, {
    headers: await authHeaders(page),
    data: { name, displayOrder: 1 },
  })
  expect(resp.status()).toBe(201)
  const { id } = await resp.json()
  return id as string
}
