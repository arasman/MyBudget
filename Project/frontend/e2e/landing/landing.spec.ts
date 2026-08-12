import { test, expect } from '@playwright/test'

/**
 * E2E: Public landing page at "/"
 *
 * Prerequisites: Docker Compose stack running.
 * Threat matrix — Routing: anonymous surface (full-stack confirmation of the
 * router-unit RED tests in src/router/__tests__/index.spec.ts, task 1.1).
 */
test.describe('Landing page (anonymous)', () => {
  test('anonymous "/" shows the landing with zero authenticated API calls and no /login bounce', async ({
    page,
  }) => {
    // Only the backend API path prefix counts — Vite dev-server module requests
    // for source files under an "api/" directory (e.g. /src/api/axios.ts,
    // /src/features/budget-structure/api/budgets.api.ts) are not backend calls
    // and must not be misclassified as authenticated API traffic.
    const apiCalls: string[] = []
    page.on('request', (req) => {
      const { pathname } = new URL(req.url())
      if (pathname.startsWith('/api/')) {
        apiCalls.push(`${req.method()} ${pathname}`)
      }
    })

    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
    await page.reload()

    await expect(page).toHaveURL('/')
    await expect(page.getByTestId('landing-view')).toBeVisible()

    // LANDING-1: LandingView must issue no authenticated API call.
    expect(apiCalls).toEqual([])
  })

  test('LandingView renders the 9-tile showcase and both CTAs', async ({ page }) => {
    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
    await page.reload()

    await expect(page.getByTestId('showcase-tile')).toHaveCount(9)
    await expect(page.locator('a[href="/register"]')).toBeVisible()
    await expect(page.locator('a[href="/login"]')).toBeVisible()
  })

  test('LanguageSwitcher works on the landing without a page reload (LANDING-6)', async ({ page }) => {
    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
    await page.reload()

    await expect(page.getByRole('heading', { level: 1 })).toBeVisible()
    const englishTitle = await page.getByRole('heading', { level: 1 }).innerText()

    await page.getByRole('button', { name: 'ES' }).click()

    await expect(page.getByRole('heading', { level: 1 })).not.toHaveText(englishTitle)
    // Same document — LanguageSwitcher must not trigger navigation/reload.
    await expect(page).toHaveURL('/')
  })

  test('signup CTA navigates to /register', async ({ page }) => {
    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
    await page.reload()

    await page.locator('a[href="/register"]').first().click()

    await expect(page).toHaveURL('/register')
  })

  test('footer is visible on the public landing and on an authenticated route (LAYOUT-4)', async ({ page }) => {
    // Public route
    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
    await page.reload()

    const currentYear = new Date().getFullYear()
    await expect(page.getByText(`© ${currentYear}`, { exact: false })).toBeVisible()

    // Authenticated route
    const email = `e2e-landing-footer-${Date.now()}@example.com`
    const password = 'Password1!'
    const resp = await page.request.post('/api/auth/register', {
      data: { email, password, firstName: 'E2E', lastName: 'Landing', preferredLocale: 'en' },
    })
    expect([201, 409]).toContain(resp.status())

    await page.goto('/login')
    await page.evaluate(() => localStorage.clear())
    await page.getByPlaceholder('you@example.com').fill(email)
    await page.getByPlaceholder('Password').fill(password)
    await page.getByRole('button', { name: 'Sign In' }).click()

    await expect(page).toHaveURL(/\/(budgets\/[^/]+\/cycles)?$/, { timeout: 10_000 })
    await expect(page.getByText(`© ${currentYear}`, { exact: false })).toBeVisible()
  })

  test('landing renders without horizontal overflow on a 375px mobile viewport (LANDING-7)', async ({
    page,
  }) => {
    await page.setViewportSize({ width: 375, height: 812 })

    await page.goto('/')
    await page.evaluate(() => localStorage.clear())
    await page.reload()

    await expect(page.getByTestId('landing-view')).toBeVisible()

    const { scrollWidth, clientWidth } = await page.evaluate(() => ({
      scrollWidth: document.documentElement.scrollWidth,
      clientWidth: document.documentElement.clientWidth,
    }))

    expect(scrollWidth).toBeLessThanOrEqual(clientWidth)
  })
})
