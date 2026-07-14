import { test, expect } from '@playwright/test'
import { seedBudgetMatrixFixture, loginWithToken, goToMatrix, createExecutionApi } from './helpers'

/**
 * E2E: BudgetMatrix currency toggle (GTQ ↔ USD).
 * Verifies that amounts convert at the cycle's exchange rate.
 *
 * REQ-MATRIX-CURRENCY
 */
test.describe('BudgetMatrix currency toggle', () => {
  test('default shows GTQ amounts', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'currency-gtq')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)

    // Create an execution of 750 GTQ for the first line in the first period
    const firstPeriodId = fixture.periodIds[0]
    const firstLineId = fixture.lineIds[0] // first line of first period
    await createExecutionApi(request, fixture.budgetId, firstPeriodId, firstLineId, fixture.accessToken, {
      entryType: 1,
      amount: 750,
    })

    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Verify default GTQ amount is shown (750.00)
    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await expect(ejecutadoCell).toBeVisible()
    const text = await ejecutadoCell.textContent()
    expect(text).toContain('750')
  })

  test('toggling to USD converts amounts', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'currency-usd')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)

    // Create 750 GTQ execution → should display as 100.00 USD (750 / 7.5)
    const firstPeriodId = fixture.periodIds[0]
    const firstLineId = fixture.lineIds[0]
    await createExecutionApi(request, fixture.budgetId, firstPeriodId, firstLineId, fixture.accessToken, {
      entryType: 1,
      amount: 750,
    })

    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Click USD toggle
    await page.getByTestId('currency-usd-btn').click()
    await page.waitForTimeout(300) // allow reactivity to update

    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    const text = await ejecutadoCell.textContent()
    expect(text).toContain('100')
  })

  test('toggling back to GTQ restores original amounts', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'currency-back-gtq')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)

    const firstPeriodId = fixture.periodIds[0]
    const firstLineId = fixture.lineIds[0]
    await createExecutionApi(request, fixture.budgetId, firstPeriodId, firstLineId, fixture.accessToken, {
      entryType: 1,
      amount: 750,
    })

    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Switch to USD
    await page.getByTestId('currency-usd-btn').click()
    await page.waitForTimeout(300)

    // Switch back to GTQ
    await page.getByTestId('currency-gtq-btn').click()
    await page.waitForTimeout(300)

    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    const text = await ejecutadoCell.textContent()
    expect(text).toContain('750')
  })
})
