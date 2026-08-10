import { test, expect } from '@playwright/test'
import { shoot, flushManifest } from './capture'
import { seedOwnerAndLogin, expectToast, dismissToasts } from './helpers'

/**
 * Slide screenshots — Budget Management: multi-budget selection/create
 * (incl. BUDGET_NAME_DUPLICATE error), soft-delete/restore, and the
 * invitation-accept round trip (success + unknown-token error).
 *
 * The invite-CREATION modal (InviteUserModal.vue) isn't wired into any route
 * yet — only its own component tests mount it — so there's no real page to
 * screenshot that flow from. The accept side (/invitations/accept) is a real,
 * routed page, seeded here the same way e2e/auth/invite-accept.spec.ts does:
 * invite via API, accept via the live UI.
 *
 * Images land in docs/slides/budget-management/.
 */
const FLOW = 'budget-management'
const MAILPIT_URL = process.env['MAILPIT_URL'] ?? 'http://localhost:8025'

async function createSecondBudget(page: import('@playwright/test').Page, accessToken: string, name: string): Promise<string> {
  const resp = await page.request.post('/api/budgets', {
    headers: { Authorization: `Bearer ${accessToken}` },
    data: { name },
  })
  expect(resp.status()).toBe(201)
  const body = await resp.json()
  return body.budgetId as string
}

test.describe('Slides — Budget Management', () => {
  test.afterAll(() => flushManifest(FLOW))

  test('budget list → create (duplicate error → success) → delete → restore', async ({ page }) => {
    const { budgetId, accessToken } = await seedOwnerAndLogin(page, 'slide-mb')
    await createSecondBudget(page, accessToken, 'Household Budget')

    await page.goto('/')
    await expect(page).toHaveURL('/', { timeout: 5_000 })
    await expect(page.locator('.card').filter({ hasText: 'Household Budget' })).toBeVisible({ timeout: 5_000 })
    await shoot(page, FLOW, 1, 'budget-list', 'Budget selection — list', 'The budget switcher, listing every budget the user belongs to.')

    // --- Create ---
    await page.getByRole('button', { name: /new budget/i }).first().click()
    const dialog = page.locator('dialog[open]')
    await expect(dialog).toBeVisible({ timeout: 3_000 })
    await dialog.getByLabel(/budget name/i).fill('Household Budget')
    await shoot(page, FLOW, 2, 'create-form', 'Create budget — form filled', 'The new-budget modal filled with a name.')

    // --- Duplicate name error ---
    await dialog.locator('button[type="submit"]').click()
    await expectToast(page, 'A budget with this name already exists')
    await shoot(page, FLOW, 3, 'create-duplicate-error', 'Create budget — duplicate name error', 'Reusing an existing budget name is rejected with BUDGET_NAME_DUPLICATE.')
    await dismissToasts(page)
    await dialog.getByRole('button', { name: 'Cancel' }).click()

    // --- Create success ---
    await page.getByRole('button', { name: /new budget/i }).first().click()
    await expect(dialog).toBeVisible({ timeout: 3_000 })
    await dialog.getByLabel(/budget name/i).fill('Vacation Fund')
    await dialog.locator('button[type="submit"]').click()
    await expect(page).toHaveURL(/\/budgets\/[^/]+\/cycles/, { timeout: 10_000 })
    await expect(page.locator('.navbar, header').getByText('Vacation Fund').first()).toBeVisible({ timeout: 5_000 })
    await shoot(page, FLOW, 4, 'create-success', 'Create budget — success', 'Creating a budget navigates straight into it — an empty cycles list, name shown in the navbar.')

    // --- Delete ---
    await page.goto('/')
    await expect(page).toHaveURL('/', { timeout: 5_000 })
    const vacationCard = page.locator('.card').filter({ hasText: 'Vacation Fund' })
    await expect(vacationCard).toBeVisible({ timeout: 5_000 })
    await vacationCard.getByRole('button', { name: /^delete$/i }).click()
    await shoot(page, FLOW, 5, 'delete-confirm', 'Delete budget — confirm dialog', 'The destructive-action confirmation before a soft-delete.')

    await page.locator('dialog.modal-open').getByRole('button', { name: /^delete$/i }).click()
    await expect(vacationCard).not.toBeVisible({ timeout: 5_000 })
    await shoot(page, FLOW, 6, 'delete-success', 'Delete budget — success', 'The deleted budget drops out of the active list.')

    // --- Restore ---
    await page.getByLabel(/show deleted/i).check()
    const deletedCard = page.locator('.card').filter({ hasText: 'Vacation Fund' })
    await expect(deletedCard).toBeVisible({ timeout: 3_000 })
    await shoot(page, FLOW, 7, 'show-deleted-toggle', 'Show deleted — toggle on', 'Toggling "Show deleted" reveals the soft-deleted budget with a Restore action.')

    await deletedCard.getByRole('button', { name: /^restore$/i }).click()
    await page.getByLabel(/show deleted/i).uncheck()
    await expect(page.locator('.card').filter({ hasText: 'Vacation Fund' })).toBeVisible({ timeout: 5_000 })
    await shoot(page, FLOW, 8, 'restore-success', 'Restore budget — success', 'The budget is active again after restore.')
  })

  test('invitation accept: success and unknown-token error', async ({ page, request }) => {
    const adminEmail = `e2e-slide-mb-admin-${Date.now()}@example.com`
    const inviteeEmail = `e2e-slide-mb-invitee-${Date.now()}@example.com`
    const password = 'Password1!'

    const adminReg = await request.post('/api/auth/register', {
      data: { email: adminEmail, password, firstName: 'E2E', lastName: 'Admin', preferredLocale: 'en' },
    })
    expect(adminReg.status()).toBe(201)
    const { accessToken: adminToken } = await adminReg.json()

    await request.post('/api/auth/register', {
      data: { email: inviteeEmail, password, firstName: 'E2E', lastName: 'Invitee', preferredLocale: 'en' },
    })

    const me = await (await request.get('/api/auth/me', { headers: { Authorization: `Bearer ${adminToken}` } })).json()
    const budgetId = me.memberships[0].budgetId

    const inviteResp = await request.post(`/api/budgets/${budgetId}/invitations`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data: { email: inviteeEmail, role: 'operator' },
    })
    expect(inviteResp.status()).toBe(201)

    // Poll Mailpit for the invite email and extract the accept token.
    const deadline = Date.now() + 15_000
    let rawToken = ''
    while (Date.now() < deadline && !rawToken) {
      const resp = await request.get(`${MAILPIT_URL}/api/v1/messages`)
      if (resp.ok()) {
        const data = await resp.json()
        const msg = (data.messages as { ID: string; Subject: string; To: { Address: string }[] }[])
          ?.find((m) => m.To.some((t) => t.Address === inviteeEmail) && m.Subject.includes('invited'))
        if (msg) {
          const body = await (await request.get(`${MAILPIT_URL}/api/v1/message/${msg.ID}`)).json()
          const text: string = body.Text || body.HTML || ''
          const match = text.match(/\/invitations\/accept\?token=([^\s"'<>]+)/)
          if (match?.[1]) rawToken = decodeURIComponent(match[1])
        }
      }
      if (!rawToken) await page.waitForTimeout(500)
    }
    expect(rawToken).toBeTruthy()

    const inviteeLogin = await request.post('/api/auth/login', { data: { email: inviteeEmail, password } })
    const { accessToken: inviteeToken, refreshToken: inviteeRefresh } = await inviteeLogin.json()

    await page.goto('/')
    await page.evaluate(
      ({ at, rt }) => {
        localStorage.setItem('accessToken', at)
        localStorage.setItem('refreshToken', rt)
      },
      { at: inviteeToken, rt: inviteeRefresh },
    )

    await page.goto(`/invitations/accept?token=${encodeURIComponent(rawToken)}`)
    await expect(page.getByText('You have successfully joined the budget.')).toBeVisible({ timeout: 10_000 })
    await shoot(page, FLOW, 9, 'invite-accept-success', 'Accept invitation — success', 'A valid invite token grants membership and shows a success message.')

    // --- Unknown token error ---
    await page.goto('/invitations/accept?token=completely-bogus-token-that-does-not-exist')
    await expect(page.getByText('An error occurred')).toBeVisible({ timeout: 5_000 })
    await shoot(page, FLOW, 10, 'invite-accept-error', 'Accept invitation — unknown token error', 'An invalid or expired token shows an error instead of a crash.')
  })
})
