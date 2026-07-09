import { test, expect } from '@playwright/test'

/**
 * E2E: Invite flow — admin invites → invitee accepts → budget accessible
 *
 * Prerequisites: Docker Compose stack running (including Mailpit on port 8025).
 * Mailpit API: http://localhost:8025/api/v1/messages
 *
 * Flow:
 *   1. Admin registers (gets default Budget + Owner membership).
 *   2. Invitee registers separately.
 *   3. Admin opens InviteUserModal and submits invite for invitee's email.
 *   4. Playwright queries Mailpit API to extract the raw token from the invite email.
 *   5. Invitee logs in, navigates to /invitations/accept?token=<raw>, asserts success.
 *   6. GET /api/auth/me confirms new BudgetMembership for invitee.
 */

const MAILPIT_URL    = process.env['MAILPIT_URL']    ?? 'http://localhost:8025'
const ADMIN_EMAIL    = `e2e-admin-${Date.now()}@example.com`
const INVITEE_EMAIL  = `e2e-invitee-${Date.now()}@example.com`
const PASSWORD       = 'Password1!'

interface MailpitMessage {
  ID: string
  Subject: string
  To: { Address: string }[]
}

interface MailpitMessages {
  messages: MailpitMessage[]
}

async function registerViaApi(page: import('@playwright/test').Page, email: string) {
  const resp = await page.request.post('/api/auth/register', {
    data: {
      email,
      password:        PASSWORD,
      firstName:       'E2E',
      lastName:        'Test',
      preferredLocale: 'en',
    },
  })
  expect(resp.status()).toBe(201)
  return resp.json()
}

async function loginViaApi(page: import('@playwright/test').Page, email: string) {
  const resp = await page.request.post('/api/auth/login', {
    data: { email, password: PASSWORD },
  })
  expect(resp.status()).toBe(200)
  return resp.json()
}

/** Poll Mailpit until an email arrives for the given recipient, return raw body. */
async function waitForEmail(
  page: import('@playwright/test').Page,
  to: string,
  timeoutMs = 15_000,
): Promise<string> {
  const deadline = Date.now() + timeoutMs
  while (Date.now() < deadline) {
    const resp = await page.request.get(`${MAILPIT_URL}/api/v1/messages`)
    if (resp.ok()) {
      const data: MailpitMessages = await resp.json()
      const msg = data.messages?.find((m) => m.To.some((t) => t.Address === to))
      if (msg) {
        const bodyResp = await page.request.get(`${MAILPIT_URL}/api/v1/message/${msg.ID}`)
        const body = await bodyResp.json()
        return body.Text ?? body.HTML ?? ''
      }
    }
    await page.waitForTimeout(500)
  }
  throw new Error(`Timeout waiting for email to ${to}`)
}

/** Extract the raw invitation token from the email body link. */
function extractToken(emailBody: string): string {
  const match = emailBody.match(/\/invitations\/accept\?token=([^\s"'<>]+)/)
  if (!match?.[1]) throw new Error('Could not find invitation token in email body')
  return decodeURIComponent(match[1])
}

test.describe('Full invite-accept round trip', () => {
  test('admin invites → invitee accepts → budget accessible', async ({ page }) => {
    // 1. Register admin and invitee
    const adminBody   = await registerViaApi(page, ADMIN_EMAIL)
    const adminToken  = adminBody.accessToken
    await registerViaApi(page, INVITEE_EMAIL)

    // 2. Get admin's budgetId via /api/auth/me
    const meResp = await page.request.get('/api/auth/me', {
      headers: { Authorization: `Bearer ${adminToken}` },
    })
    expect(meResp.status()).toBe(200)
    const meBody = await meResp.json()
    const budgetId = meBody.memberships[0].budgetId
    expect(budgetId).toBeTruthy()

    // 3. Admin sends invitation via API (UI invite tested in component test 6.5)
    const inviteResp = await page.request.post(`/api/budgets/${budgetId}/invitations`, {
      headers: { Authorization: `Bearer ${adminToken}` },
      data:    { email: INVITEE_EMAIL, role: 'operator' },
    })
    expect(inviteResp.status()).toBe(201)

    // 4. Extract token from Mailpit
    const emailBody = await waitForEmail(page, INVITEE_EMAIL)
    const rawToken  = extractToken(emailBody)

    // 5. Invitee logs in via UI and navigates to accept page
    const inviteeBody  = await loginViaApi(page, INVITEE_EMAIL)
    const inviteeToken = inviteeBody.accessToken

    // Set invitee tokens in localStorage before navigating
    await page.goto('/')
    await page.evaluate(
      ({ at, rt }) => {
        localStorage.setItem('accessToken', at)
        localStorage.setItem('refreshToken', rt)
      },
      { at: inviteeToken, rt: inviteeBody.refreshToken },
    )

    await page.goto(`/invitations/accept?token=${encodeURIComponent(rawToken)}`)

    // 6. Expect success message
    await expect(
      page.getByText('You have successfully joined the budget.'),
    ).toBeVisible({ timeout: 10_000 })

    // 7. Confirm via /api/auth/me that membership was added
    const inviteeMeResp = await page.request.get('/api/auth/me', {
      headers: { Authorization: `Bearer ${inviteeToken}` },
    })
    expect(inviteeMeResp.status()).toBe(200)
    const inviteeMeBody = await inviteeMeResp.json()
    const membership    = inviteeMeBody.memberships.find(
      (m: { budgetId: string; role: string }) => m.budgetId === budgetId,
    )
    expect(membership).toBeTruthy()
    expect(membership.role).toBe('operator')
  })

  test('invitee sees error message for unknown token', async ({ page }) => {
    // Register invitee
    const body  = await registerViaApi(page, `e2e-unknown-${Date.now()}@example.com`)
    const token = body.accessToken

    await page.goto('/')
    await page.evaluate(
      ({ at, rt }) => {
        localStorage.setItem('accessToken', at)
        localStorage.setItem('refreshToken', rt)
      },
      { at: token, rt: body.refreshToken },
    )

    await page.goto('/invitations/accept?token=completely-bogus-token-that-does-not-exist')

    await expect(page.getByText('An error occurred')).toBeVisible({ timeout: 5_000 })
  })

  test('unauthenticated user is redirected to /login with token preserved', async ({ page }) => {
    await page.goto('/')  // must navigate first — localStorage inaccessible on about:blank
    await page.evaluate(() => localStorage.clear())
    await page.goto('/invitations/accept?token=some-token')

    await expect(page).toHaveURL(/\/login/, { timeout: 5_000 })
    // The redirect query param should contain the token
    const url = page.url()
    expect(url).toContain('some-token')
  })
})
