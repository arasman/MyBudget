import { test, expect } from '@playwright/test'

/**
 * E2E: Register → auto-login → home
 *
 * Prerequisites: Docker Compose stack running (backend, frontend dev server, Postgres, Redis).
 * The test uses a unique timestamped email to avoid conflicts across runs.
 */
test.describe('Register flow', () => {
  test('valid registration redirects to home and stores tokens', async ({ page }) => {
    const email     = `e2e-register-${Date.now()}@example.com`
    const password  = 'Password1!'
    const firstName = 'E2E'
    const lastName  = 'Register'

    await page.goto('/register')

    await page.getByPlaceholder('First name').fill(firstName)
    await page.getByPlaceholder('Last name').fill(lastName)
    await page.getByPlaceholder('you@example.com').fill(email)
    await page.getByPlaceholder(/Password/).fill(password)

    await page.getByRole('button', { name: 'Create Account' }).click()

    // Should redirect to home after successful registration
    await expect(page).toHaveURL(/\/(budgets\/[^/]+\/cycles)?$/, { timeout: 10_000 })

    // Tokens must be persisted in localStorage
    const accessToken = await page.evaluate(() => localStorage.getItem('accessToken'))
    expect(accessToken).toBeTruthy()

    const refreshToken = await page.evaluate(() => localStorage.getItem('refreshToken'))
    expect(refreshToken).toBeTruthy()

    // The page should not show any error
    await expect(page.getByRole('alert')).not.toBeVisible()
  })

  test('duplicate email shows 409 error', async ({ page }) => {
    const email    = `e2e-dup-${Date.now()}@example.com`
    const password = 'Password1!'

    // First registration — should succeed
    await page.goto('/register')
    await page.getByPlaceholder('First name').fill('First')
    await page.getByPlaceholder('Last name').fill('User')
    await page.getByPlaceholder('you@example.com').fill(email)
    await page.getByPlaceholder(/Password/).fill(password)
    await page.getByRole('button', { name: 'Create Account' }).click()
    await expect(page).toHaveURL(/\/(budgets\/[^/]+\/cycles)?$/, { timeout: 10_000 })

    // Clear storage and try to register again with the same email
    await page.evaluate(() => {
      localStorage.clear()
    })
    await page.goto('/register')
    await page.getByPlaceholder('First name').fill('Second')
    await page.getByPlaceholder('Last name').fill('User')
    await page.getByPlaceholder('you@example.com').fill(email)
    await page.getByPlaceholder(/Password/).fill(password)
    await page.getByRole('button', { name: 'Create Account' }).click()

    // Should stay on /register and show an error
    await expect(page).toHaveURL('/register')
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5_000 })
  })
})
