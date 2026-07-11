import type { Page } from '@playwright/test'
import { expect } from '@playwright/test'

const PASSWORD = 'Password1!'

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
  const email = `e2e-${prefix}-${Date.now()}@example.com`

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
