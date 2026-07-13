import { test, expect } from '@playwright/test'

/**
 * E2E: Password management flows
 *
 * Prerequisites: Docker Compose stack running (including Mailpit on :8025).
 * Uses freshly registered accounts — each describe block seeds its own user.
 */

const PASSWORD = 'Password1!'
const NEW_PASSWORD = 'NewPassword2!'

async function seedUser(
  page: import('@playwright/test').Page,
  email: string,
) {
  const resp = await page.request.post('/api/auth/register', {
    data: {
      email,
      password: PASSWORD,
      firstName: 'E2E',
      lastName: 'PwdTest',
      preferredLocale: 'en',
    },
  })
  expect([201, 409]).toContain(resp.status())
  await page.goto('/login')
  await page.evaluate(() => localStorage.clear())
}

async function loginAs(
  page: import('@playwright/test').Page,
  email: string,
  password: string,
) {
  const resp = await page.request.post('/api/auth/login', {
    data: { email, password },
  })
  if (resp.status() !== 200) return false
  const body = await resp.json()
  await page.goto('/')
  await page.evaluate(
    ({ at, rt }) => {
      localStorage.setItem('accessToken', at)
      localStorage.setItem('refreshToken', rt)
    },
    { at: body.accessToken, rt: body.refreshToken },
  )
  return true
}

test.describe('Forgot-password flow', () => {
  const EMAIL = `e2e-forgot-${Date.now()}@example.com`

  test.beforeEach(async ({ page }) => {
    await seedUser(page, EMAIL)
  })

  test('navigate to /forgot-password, submit email, see success message', async ({ page }) => {
    await page.goto('/forgot-password')

    await expect(page.getByText('Forgot your password?')).toBeVisible()

    const emailInput = page.locator('input[type="email"]')
    await emailInput.fill(EMAIL)
    await page.getByRole('button', { name: /send reset link/i }).click()

    await expect(
      page.getByText(/If your email is registered, a reset link has been sent/i),
    ).toBeVisible({ timeout: 10_000 })
  })

  test('shows force-change banner when ?reason=force is present', async ({ page }) => {
    await page.goto('/forgot-password?reason=force')
    await expect(
      page.getByText(/Your password has expired and must be changed/i),
    ).toBeVisible()
  })
})

test.describe('Reset-password — invalid token', () => {
  test('navigate to /reset-password?token=invalid, submit, see error', async ({ page }) => {
    await page.goto('/reset-password?token=invalid-token-that-does-not-exist')

    const passwordInputs = page.locator('input[type="password"]')
    await passwordInputs.nth(0).fill('NewPassword1!')
    await passwordInputs.nth(1).fill('NewPassword1!')

    await page.getByRole('button', { name: /reset password/i }).click()

    await expect(
      page.getByText(/This reset link is invalid or has expired/i),
    ).toBeVisible({ timeout: 10_000 })

    // Link back to forgot-password should be present
    await expect(page.getByRole('link', { name: /send reset link/i })).toBeVisible()
  })
})

test.describe('Change password from AppLayout', () => {
  const EMAIL = `e2e-change-pwd-${Date.now()}@example.com`

  test.beforeEach(async ({ page }) => {
    await seedUser(page, EMAIL)
  })

  test('login → open dropdown → click Change password → fill modal → submit → success notification', async ({ page }) => {
    const ok = await loginAs(page, EMAIL, PASSWORD)
    if (!ok) {
      test.skip()
      return
    }

    // Navigate to authenticated area
    await page.goto('/')
    await page.waitForURL(/\//, { timeout: 10_000 })

    // Open user avatar dropdown (daisyUI CSS-only dropdown)
    await page.locator('.avatar.placeholder').click()

    // Click "Change password" menu item
    await page.getByRole('button', { name: /change password/i }).click()

    // Modal should be open — fill fields
    const passwordInputs = page.locator('dialog[open] input[type="password"]')
    await passwordInputs.nth(0).fill(PASSWORD)        // current password
    await passwordInputs.nth(1).fill(NEW_PASSWORD)    // new password
    await passwordInputs.nth(2).fill(NEW_PASSWORD)    // confirm

    await page.locator('dialog[open]').getByRole('button', { name: /change password/i }).click()

    // Modal closes on success — open notification bell (not the avatar) to reveal the notification
    await page.locator('label.btn-circle:not(.avatar)').click()
    await expect(
      page.getByText('Password changed successfully.'),
    ).toBeVisible({ timeout: 10_000 })
  })
})
