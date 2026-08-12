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

    // exact: true — LANDING-9 gives showcase tiles accessible names like
    // "Enlarge Plan in cycles", whose "...cycles" substring fuzzy-matches an
    // un-exact { name: 'ES' } query too (Playwright name matching is
    // substring/case-insensitive by default).
    await page.getByRole('button', { name: 'ES', exact: true }).click()

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

  test.describe('Showcase tile enlarge on interaction (LANDING-9)', () => {
    test('hovering a tile past the dwell delay enlarges it to the grid-container width and dims the other 8 (dwell hover)', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 1280, height: 800 })
      await page.goto('/')
      await page.evaluate(() => localStorage.clear())
      await page.reload()

      const tiles = page.getByTestId('showcase-tile')
      await expect(tiles).toHaveCount(9)

      const grid = page.getByTestId('flow-showcase-grid')
      const gridBox = await grid.boundingBox()
      expect(gridBox).not.toBeNull()

      const first = tiles.first()
      await first.hover()
      // Past the ~175ms dwell delay AND the 180ms width/left CSS transition
      // (main.css .showcase-zoom-card) — otherwise boundingBox() below reads
      // a mid-transition frame, not the settled geometry.
      await page.waitForTimeout(500)

      await expect(first).toHaveClass(/showcase-zoom-card/)
      const tileBox = await first.boundingBox()
      expect(tileBox).not.toBeNull()
      expect(Math.round(tileBox!.width)).toBeCloseTo(Math.round(gridBox!.width), -1)

      const second = tiles.nth(1)
      await expect(second).toHaveJSProperty('inert', true)
      await expect(second).toHaveAttribute('aria-hidden', 'true')
    })

    test('a pointer sweep across tiles without dwelling on any single one enlarges none of them (sweep without dwell)', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 1280, height: 800 })
      await page.goto('/')
      await page.evaluate(() => localStorage.clear())
      await page.reload()

      const tiles = page.getByTestId('showcase-tile')
      for (let i = 0; i < 4; i++) {
        await tiles.nth(i).hover()
        await page.waitForTimeout(50) // well under the ~175ms dwell
      }

      for (let i = 0; i < 4; i++) {
        await expect(tiles.nth(i)).not.toHaveClass(/showcase-zoom-card/)
      }
    })

    test('Tab enlarges a tile immediately (no wait), synchronized with the focus ring; Escape dismisses it and focus stays reachable', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 1280, height: 800 })
      await page.goto('/')
      await page.evaluate(() => localStorage.clear())
      await page.reload()

      const first = page.getByTestId('showcase-tile').first()
      await first.focus()

      await expect(first).toHaveClass(/showcase-zoom-card/)
      await expect(first).toBeFocused()

      await page.keyboard.press('Escape')

      await expect(first).not.toHaveClass(/showcase-zoom-card/)
      const activeTag = await page.evaluate(() => document.activeElement?.tagName ?? null)
      expect(activeTag).not.toBeNull()
    })

    test('no horizontal overflow while a tile is active at 1280x800 (LANDING-7 regression, active state)', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 1280, height: 800 })
      await page.goto('/')
      await page.evaluate(() => localStorage.clear())
      await page.reload()

      const first = page.getByTestId('showcase-tile').first()
      await first.click()
      await expect(first).toHaveClass(/showcase-zoom-card/)
      await page.waitForTimeout(200) // past the 180ms width/left transition

      const { scrollWidth, clientWidth } = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        clientWidth: document.documentElement.clientWidth,
      }))

      expect(scrollWidth).toBeLessThanOrEqual(clientWidth)
    })

    test('below the sm: breakpoint (375px) hovering/tapping a tile never enlarges it and LANDING-7 still holds', async ({
      page,
    }) => {
      await page.setViewportSize({ width: 375, height: 812 })
      await page.goto('/')
      await page.evaluate(() => localStorage.clear())
      await page.reload()

      const first = page.getByTestId('showcase-tile').first()
      await first.hover()
      await page.waitForTimeout(300)
      // Touch is out of scope for the chromium project (no hasTouch context);
      // a click stands in for "tap" here — both must be no-ops below sm:.
      await first.click()
      await page.waitForTimeout(100)

      await expect(first).not.toHaveClass(/showcase-zoom-card/)

      const { scrollWidth, clientWidth } = await page.evaluate(() => ({
        scrollWidth: document.documentElement.scrollWidth,
        clientWidth: document.documentElement.clientWidth,
      }))

      expect(scrollWidth).toBeLessThanOrEqual(clientWidth)
    })

    test('respects prefers-reduced-motion: reduce by disabling the enlarge transition entirely', async ({
      page,
    }) => {
      // Must be set before navigation — emulateMedia affects the page's media
      // query evaluation for the whole session, including the initial load.
      await page.emulateMedia({ reducedMotion: 'reduce' })
      await page.setViewportSize({ width: 1280, height: 800 })
      await page.goto('/')
      await page.evaluate(() => localStorage.clear())
      await page.reload()

      const first = page.getByTestId('showcase-tile').first()
      await first.click()
      await expect(first).toHaveClass(/showcase-zoom-card/)

      // main.css's @media (prefers-reduced-motion: reduce) block sets
      // `.showcase-zoom-card { transition: none }`, which collapses the
      // computed transition-duration list down to the single initial value
      // '0s' (the non-reduced-motion rule declares three durations —
      // opacity/width/left — each 180ms, so this genuinely distinguishes
      // "no transition" from an unnoticed instant transition).
      const transitionDuration = await first.evaluate((el) => getComputedStyle(el).transitionDuration)
      expect(transitionDuration).toBe('0s')

      const transitionProperty = await first.evaluate((el) => getComputedStyle(el).transitionProperty)
      expect(transitionProperty).toBe('none')
    })
  })
})
