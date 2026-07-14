import { test, expect } from '@playwright/test'
import { seedBudgetMatrixFixture, loginWithToken, goToMatrix } from './helpers'

/**
 * E2E: BudgetMatrix period navigation.
 * Verifies the 3-column sliding window (prev / next) behaviour.
 *
 * REQ-MATRIX-NAV
 */
test.describe('BudgetMatrix navigation', () => {
  test('shows 3 period columns on initial load', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'nav-init')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Each period header cell carries a data-testid="period-header"
    const periodHeaders = page.locator('[data-testid="period-header"]')
    await expect(periodHeaders).toHaveCount(3)
  })

  test('next period button shifts window', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'nav-next')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // On initial load periods 1, 2, 3 are visible (January, February, March)
    await expect(page.locator('[data-testid="period-header"]').first()).toContainText('January')

    // Click next
    await page.getByTestId('period-next-btn').click()
    await page.waitForLoadState('networkidle')

    // Now periods 2, 3, 4 are visible — April should be the last visible header
    const headers = page.locator('[data-testid="period-header"]')
    await expect(headers).toHaveCount(3)
    await expect(headers.last()).toContainText('April')

    // January should no longer be visible
    await expect(page.locator('[data-testid="period-header"]').filter({ hasText: 'January' })).toHaveCount(0)
  })

  test('prev button disabled on first period window', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'nav-prev-disabled')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    const prevBtn = page.getByTestId('period-prev-btn')
    await expect(prevBtn).toBeDisabled()
  })

  test('next button disabled at last period window', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'nav-next-disabled')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Advance to the last window (4 periods → window [1,2,3] then [2,3,4])
    await page.getByTestId('period-next-btn').click()
    await page.waitForLoadState('networkidle')

    const nextBtn = page.getByTestId('period-next-btn')
    await expect(nextBtn).toBeDisabled()
  })
})
