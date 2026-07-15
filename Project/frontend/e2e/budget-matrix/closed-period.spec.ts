import { test, expect } from '@playwright/test'
import { seedBudgetMatrixFixture, loginWithToken, goToMatrix, closePeriodApi } from './helpers'

/**
 * E2E: BudgetMatrix closed period behaviour.
 * Verifies the RefreshIcon appears in closed period columns and that the
 * execution modal is read-only for closed periods.
 *
 * REQ-MATRIX-EXEC, REQ-MATRIX-REFRESH
 */
test.describe('BudgetMatrix closed period', () => {
  test('closed period column shows refresh icon', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'closed-refresh')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)

    // Close the first period
    await closePeriodApi(
      request,
      fixture.budgetId,
      fixture.cycleId,
      fixture.periodIds[0],
      fixture.accessToken,
    )

    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // The refresh icon should be visible in the first period column header
    const refreshIcon = page.locator('[data-testid="period-refresh-icon"]').first()
    await expect(refreshIcon).toBeVisible()
  })

  test('open period does not show refresh icon', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'open-no-refresh')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)

    // Close only the second period — first and third remain open
    await closePeriodApi(
      request,
      fixture.budgetId,
      fixture.cycleId,
      fixture.periodIds[1],
      fixture.accessToken,
    )

    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // There should be exactly 1 refresh icon (for period 2)
    const refreshIcons = page.locator('[data-testid="period-refresh-icon"]')
    await expect(refreshIcons).toHaveCount(1)
  })

  test('double-click on closed period Ejecutado shows read-only modal', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'closed-readonly-modal')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)

    // Close the first period
    await closePeriodApi(
      request,
      fixture.budgetId,
      fixture.cycleId,
      fixture.periodIds[0],
      fixture.accessToken,
    )

    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Double-click the first Ejecutado cell (which belongs to the closed period)
    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await expect(ejecutadoCell).toBeVisible()
    await ejecutadoCell.dispatchEvent('dblclick')

    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()

    // The Add Entry form should be absent in a closed period modal
    await expect(modal.locator('[data-testid="execution-record-form"]')).toHaveCount(0)

    // A closed-period banner / alert should be visible
    const closedBanner = modal.locator('[data-testid="closed-period-banner"]')
    await expect(closedBanner).toBeVisible()
  })
})
