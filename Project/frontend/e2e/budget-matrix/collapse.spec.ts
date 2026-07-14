import { test, expect } from '@playwright/test'
import { seedBudgetMatrixFixture, loginWithToken, goToMatrix } from './helpers'

/**
 * E2E: BudgetMatrix group/category collapse and expand.
 *
 * REQ-MATRIX-STRUCT
 */
test.describe('BudgetMatrix collapse/expand', () => {
  test('collapsing a group hides its category rows', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'collapse-group')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Wait for the first group row to be visible
    const groupRow = page.locator('[data-testid="matrix-group-row"]').first()
    await expect(groupRow).toBeVisible()

    // Category rows should be visible initially
    const categoryRow = page.locator('[data-testid="matrix-category-row"]').first()
    await expect(categoryRow).toBeVisible()

    // Click the collapse toggle on the first group
    await groupRow.getByTestId('group-collapse-btn').click()

    // Category row should now be hidden
    await expect(categoryRow).toBeHidden()
  })

  test('expanding a group shows category rows again', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'expand-group')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    const groupRow = page.locator('[data-testid="matrix-group-row"]').first()
    await expect(groupRow).toBeVisible()

    const categoryRow = page.locator('[data-testid="matrix-category-row"]').first()

    // Collapse first
    await groupRow.getByTestId('group-collapse-btn').click()
    await expect(categoryRow).toBeHidden()

    // Expand again
    await groupRow.getByTestId('group-collapse-btn').click()
    await expect(categoryRow).toBeVisible()
  })
})
