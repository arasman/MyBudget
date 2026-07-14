import { test, expect } from '@playwright/test'
import { seedBudgetMatrixFixture, loginWithToken, goToMatrix, deleteGroupApi } from './helpers'

/**
 * E2E: BudgetMatrix "include deleted" toggle.
 * Verifies that soft-deleted groups are hidden by default and shown (in gray)
 * when the "Incluir eliminados" checkbox is checked.
 *
 * REQ-MATRIX-DELETED
 */
test.describe('BudgetMatrix include deleted', () => {
  test('deleted group hidden by default', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'deleted-hidden')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)

    // Delete the first group via API
    await deleteGroupApi(request, fixture.budgetId, fixture.groupIds[0], fixture.accessToken)

    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // There should be only 1 group row visible (the second group remains)
    const groupRows = page.locator('[data-testid="matrix-group-row"]')
    await expect(groupRows).toHaveCount(1)

    // Specifically the Housing group row should be absent
    await expect(page.locator('[data-testid="matrix-group-row"]').filter({ hasText: 'Housing' })).toHaveCount(0)
  })

  test('checking include-deleted shows deleted group in gray', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'deleted-shown')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)

    // Delete the first group via API
    await deleteGroupApi(request, fixture.budgetId, fixture.groupIds[0], fixture.accessToken)

    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Check "Incluir eliminados" checkbox
    const checkbox = page.getByTestId('include-deleted-checkbox')
    await checkbox.check()
    await page.waitForLoadState('networkidle')

    // Both groups should now be visible
    const groupRows = page.locator('[data-testid="matrix-group-row"]')
    await expect(groupRows).toHaveCount(2)

    // The deleted group should have a CSS class indicating gray / deleted styling
    const deletedGroupRow = page.locator('[data-testid="matrix-group-row"]').filter({ hasText: 'Housing' })
    await expect(deletedGroupRow).toBeVisible()
    // Verify it carries a visual deleted indicator (opacity class or text-gray class)
    await expect(deletedGroupRow).toHaveClass(/opacity|gray|deleted/)
  })
})
