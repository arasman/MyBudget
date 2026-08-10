import { test, expect } from '@playwright/test'
import { shoot, flushManifest } from './capture'

/**
 * Slide screenshots — Auth flow (register, login, error states, logout).
 * Images land in docs/slides/auth/; run `pnpm slides:index` after to (re)build
 * docs/slides/auth/index.md and the root docs/slides/index.md.
 */
const FLOW = 'auth'
const PASSWORD = 'Password1!'

test.describe('Slides — Auth', () => {
  test.afterAll(() => flushManifest(FLOW))

  test('register — empty form, success, duplicate email error', async ({ page }) => {
    const email = `e2e-slide-register-${Date.now()}@example.com`

    await page.goto('/register')
    await shoot(page, FLOW, 1, 'register-empty', 'Register — empty form', 'The registration form before any input.')

    await page.getByPlaceholder('First name').fill('E2E')
    await page.getByPlaceholder('Last name').fill('Slides')
    await page.getByPlaceholder('you@example.com').fill(email)
    await page.getByPlaceholder(/Password/).fill(PASSWORD)
    await shoot(page, FLOW, 2, 'register-filled', 'Register — filled form', 'The registration form filled with valid data, ready to submit.')

    await page.getByRole('button', { name: 'Create Account' }).click()
    await expect(page).toHaveURL(/\/(budgets\/[^/]+\/cycles)?$/, { timeout: 10_000 })
    await shoot(page, FLOW, 3, 'register-success', 'Register — success', 'After submit: auto-login and redirect to the home/cycles view.')

    // Duplicate email — register the same address again
    await page.evaluate(() => localStorage.clear())
    await page.goto('/register')
    await page.getByPlaceholder('First name').fill('Second')
    await page.getByPlaceholder('Last name').fill('User')
    await page.getByPlaceholder('you@example.com').fill(email)
    await page.getByPlaceholder(/Password/).fill(PASSWORD)
    await page.getByRole('button', { name: 'Create Account' }).click()
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5_000 })
    await shoot(page, FLOW, 4, 'register-duplicate-error', 'Register — duplicate email error', 'Submitting an already-registered email stays on the form and shows a 409 error.')
  })

  test('login — success and invalid credentials error', async ({ page }) => {
    const email = `e2e-slide-login-${Date.now()}@example.com`
    const resp = await page.request.post('/api/auth/register', {
      data: { email, password: PASSWORD, firstName: 'E2E', lastName: 'Login', preferredLocale: 'en' },
    })
    expect(resp.status()).toBe(201)

    await page.goto('/login')
    await page.evaluate(() => localStorage.clear())
    await page.goto('/login')
    await shoot(page, FLOW, 5, 'login-empty', 'Login — empty form', 'The login form before any input.')

    await page.getByPlaceholder('you@example.com').fill(email)
    await page.getByPlaceholder('Password').fill(PASSWORD)
    await page.getByRole('button', { name: 'Sign In' }).click()
    await expect(page).toHaveURL(/\/(budgets\/[^/]+\/cycles)?$/, { timeout: 10_000 })
    await shoot(page, FLOW, 6, 'login-success', 'Login — success', 'Valid credentials redirect to the home/cycles view.')

    // Invalid credentials
    await page.evaluate(() => localStorage.clear())
    await page.goto('/login')
    await page.getByPlaceholder('you@example.com').fill(email)
    await page.getByPlaceholder('Password').fill('WrongPassword1!')
    await page.getByRole('button', { name: 'Sign In' }).click()
    await expect(page.getByRole('alert')).toBeVisible({ timeout: 5_000 })
    await shoot(page, FLOW, 7, 'login-invalid-error', 'Login — invalid credentials error', 'Wrong password stays on /login and shows an error alert.')
  })

  test('logout — clears session and returns to login', async ({ page }) => {
    const email = `e2e-slide-logout-${Date.now()}@example.com`
    const resp = await page.request.post('/api/auth/register', {
      data: { email, password: PASSWORD, firstName: 'E2E', lastName: 'Logout', preferredLocale: 'en' },
    })
    expect(resp.status()).toBe(201)

    await page.goto('/login')
    await page.getByPlaceholder('you@example.com').fill(email)
    await page.getByPlaceholder('Password').fill(PASSWORD)
    await page.getByRole('button', { name: 'Sign In' }).click()
    await expect(page).toHaveURL(/\/(budgets\/[^/]+\/cycles)?$/, { timeout: 10_000 })

    await page.locator('.avatar.placeholder').click()
    await shoot(page, FLOW, 8, 'logout-menu', 'Logout — user menu', 'The account dropdown with the logout action, opened from the avatar.')

    await page.getByRole('button', { name: /logout|sign out/i }).click()
    await expect(page).toHaveURL('/login', { timeout: 5_000 })
    await shoot(page, FLOW, 9, 'logout-success', 'Logout — success', 'Session cleared, back on /login.')
  })
})
