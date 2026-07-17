import { test, expect } from '@playwright/test'

/**
 * E2E: Multi-budget — create, delete (soft), restore, navigation guard.
 *
 * Prerequisites: Docker Compose stack running + backend + frontend dev servers.
 * Each test registers a fresh user (auto-creates one budget on registration).
 */

const PASSWORD = 'Password1!'

async function seedOwnerAndLogin(page: import('@playwright/test').Page, prefix = 'mb') {
  const email = `e2e-${prefix}-${Date.now()}@example.com`

  const regResp = await page.request.post('/api/auth/register', {
    data: {
      email,
      password:        PASSWORD,
      firstName:       'E2E',
      lastName:        'MultiB',
      preferredLocale: 'en',
    },
  })
  expect(regResp.status()).toBe(201)
  const regBody = await regResp.json()

  // Inject tokens into browser localStorage (must navigate first so localStorage is accessible)
  await page.goto('/login')
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
  const membership = me.memberships[0]

  return {
    email,
    accessToken: regBody.accessToken as string,
    budgetId:    membership.budgetId as string,
    budgetName:  membership.budgetName as string,
  }
}

/** Create a second budget via API so BudgetSelectionView doesn't auto-redirect (needs >= 2 active). */
async function createSecondBudget(
  page: import('@playwright/test').Page,
  accessToken: string,
  name: string,
): Promise<string> {
  const resp = await page.request.post('/api/budgets', {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: { name },
  })
  expect(resp.status()).toBe(201)
  const body = await resp.json()
  return body.budgetId as string
}

// ─── Create ─────────────────────────────────────────────────────────────────

test.describe('Multi-budget — create', () => {
  test('creates a new budget and navigates to it', async ({ page }) => {
    const { accessToken } = await seedOwnerAndLogin(page, 'mb-create')

    // Create a second budget so BudgetSelectionView has 2 → no auto-redirect
    await createSecondBudget(page, accessToken, 'Anchor Budget')

    // Now navigate to selection view — 2 budgets, no auto-redirect
    await page.goto('/')
    await expect(page).toHaveURL('/', { timeout: 5_000 })

    // Click "New Budget" button to open modal
    await page.getByRole('button', { name: /new budget/i }).first().click()
    await expect(page.locator('dialog[open]')).toBeVisible({ timeout: 3_000 })

    // Fill budget name and submit
    await page.locator('dialog[open]').getByLabel(/budget name/i).fill('E2E Third Budget')
    await page.locator('dialog[open]').locator('button[type="submit"]').click()

    // Should navigate to the new budget's cycles view
    await expect(page).toHaveURL(/\/budgets\/[^/]+\/cycles/, { timeout: 10_000 })

    // Budget name visible in navbar
    await expect(
      page.locator('.navbar, header').getByText('E2E Third Budget').first()
    ).toBeVisible({ timeout: 5_000 })
  })

  test('empty name shows validation error, modal stays open', async ({ page }) => {
    const { accessToken } = await seedOwnerAndLogin(page, 'mb-create-val')
    await createSecondBudget(page, accessToken, 'Anchor Budget')

    await page.goto('/')
    await expect(page).toHaveURL('/', { timeout: 5_000 })

    await page.getByRole('button', { name: /new budget/i }).first().click()
    await expect(page.locator('dialog[open]')).toBeVisible({ timeout: 3_000 })

    // Submit without filling name
    await page.locator('dialog[open]').locator('button[type="submit"]').click()

    // Validation error — modal stays open, no navigation
    await expect(page.locator('dialog[open]')).toBeVisible({ timeout: 3_000 })
    await expect(page).not.toHaveURL(/\/budgets\/[^/]+\/cycles/)
  })
})

// ─── Delete (soft) ──────────────────────────────────────────────────────────

test.describe('Multi-budget — delete (soft)', () => {
  test('owner can soft-delete a budget; deleted budget disappears from active list', async ({ page }) => {
    const { accessToken } = await seedOwnerAndLogin(page, 'mb-delete')
    const targetId = await createSecondBudget(page, accessToken, 'Budget To Delete')

    // 2 budgets → no auto-redirect
    await page.goto('/')
    await expect(page).toHaveURL('/', { timeout: 5_000 })

    const budgetCard = page.locator('.card').filter({ hasText: 'Budget To Delete' })
    await expect(budgetCard).toBeVisible({ timeout: 5_000 })

    await budgetCard.getByRole('button', { name: /^delete$/i }).click()
    await page.locator('dialog.modal-open').getByRole('button', { name: /^delete$/i }).click()

    // Budget disappears from active list
    await expect(budgetCard).not.toBeVisible({ timeout: 5_000 })

    // Navigating directly to deleted budget redirects to /
    await page.goto(`/budgets/${targetId}/cycles`)
    await expect(page).toHaveURL('/', { timeout: 5_000 })
  })
})

// ─── Restore ────────────────────────────────────────────────────────────────

test.describe('Multi-budget — restore', () => {
  test('owner can restore a soft-deleted budget via show-deleted toggle', async ({ page }) => {
    const { accessToken } = await seedOwnerAndLogin(page, 'mb-restore')
    const targetId = await createSecondBudget(page, accessToken, 'Budget To Restore')

    await page.goto('/')
    await expect(page).toHaveURL('/', { timeout: 5_000 })

    // Delete via UI
    const budgetCard = page.locator('.card').filter({ hasText: 'Budget To Restore' })
    await expect(budgetCard).toBeVisible({ timeout: 5_000 })
    await budgetCard.getByRole('button', { name: /^delete$/i }).click()
    await page.locator('dialog.modal-open').getByRole('button', { name: /^delete$/i }).click()
    await expect(budgetCard).not.toBeVisible({ timeout: 5_000 })

    // Toggle show deleted
    await page.getByLabel(/show deleted/i).check()
    const deletedCard = page.locator('.card').filter({ hasText: 'Budget To Restore' })
    await expect(deletedCard).toBeVisible({ timeout: 3_000 })

    // Restore
    await deletedCard.getByRole('button', { name: /^restore$/i }).click()

    // Budget back in active list (without show-deleted)
    await page.getByLabel(/show deleted/i).uncheck()
    await expect(page.locator('.card').filter({ hasText: 'Budget To Restore' })).toBeVisible({ timeout: 5_000 })

    // Navigate to restored budget — no redirect
    await page.goto(`/budgets/${targetId}/cycles`)
    await expect(page).toHaveURL(`/budgets/${targetId}/cycles`, { timeout: 10_000 })
  })
})

// ─── Navigation guard ───────────────────────────────────────────────────────

test.describe('Multi-budget — navigation guard', () => {
  test('navigating to a deleted budget URL redirects to /', async ({ page }) => {
    const { accessToken } = await seedOwnerAndLogin(page, 'mb-guard')
    const targetId = await createSecondBudget(page, accessToken, 'Guard Test Budget')

    await page.goto('/')
    await expect(page).toHaveURL('/', { timeout: 5_000 })

    const budgetCard = page.locator('.card').filter({ hasText: 'Guard Test Budget' })
    await expect(budgetCard).toBeVisible({ timeout: 5_000 })
    await budgetCard.getByRole('button', { name: /^delete$/i }).click()
    await page.locator('dialog.modal-open').getByRole('button', { name: /^delete$/i }).click()
    await expect(budgetCard).not.toBeVisible({ timeout: 5_000 })

    // Navigate directly to deleted budget URL
    await page.goto(`/budgets/${targetId}/cycles`)
    await expect(page).toHaveURL('/', { timeout: 5_000 })
  })

  test('navigating to an active budget does NOT redirect', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'mb-guard-active')

    await page.goto(`/budgets/${budgetId}/cycles`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/cycles`, { timeout: 10_000 })
  })
})

// ─── activeBudgetName on page reload ────────────────────────────────────────

test.describe('Multi-budget — activeBudgetName on page reload', () => {
  test('budget name in navbar survives a page reload', async ({ page }) => {
    const { budgetId, budgetName } = await seedOwnerAndLogin(page, 'mb-reload')

    await page.goto(`/budgets/${budgetId}/cycles`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/cycles`, { timeout: 10_000 })

    await page.reload()
    await expect(
      page.locator('.navbar, header').getByText(budgetName).first()
    ).toBeVisible({ timeout: 5_000 })
  })
})
