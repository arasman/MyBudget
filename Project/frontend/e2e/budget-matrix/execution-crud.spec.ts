import { test, expect } from '@playwright/test'
import { seedBudgetMatrixFixture, loginWithToken, goToMatrix } from './helpers'

/**
 * E2E: BudgetMatrix execution record CRUD through the modal.
 *
 * REQ-MATRIX-EXEC, REQ-MATRIX-TOTALS
 */
test.describe('BudgetMatrix execution CRUD', () => {
  test('double-click on Ejecutado cell opens execution modal', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'crud-modal-open')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Double-click the first Ejecutado cell in the matrix
    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await expect(ejecutadoCell).toBeVisible()
    await ejecutadoCell.dispatchEvent('dblclick')

    // Modal should appear
    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()
  })

  test('creating an Expense record updates the executed total', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'crud-create')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await expect(ejecutadoCell).toBeVisible()

    // Open modal via double-click
    await ejecutadoCell.dispatchEvent('dblclick')
    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()

    // Fill the form — select Expense, enter amount 100
    await modal.locator('[data-testid="entry-type-select"]').selectOption({ label: 'Expense' })
    await modal.locator('[data-testid="amount-input"]').fill('100')
    await modal.locator('#exec-note').fill('E2E test expense')

    // Submit
    const [createResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/executions') && r.request().method() === 'POST'),
      modal.locator('[data-testid="execution-form-submit"]').click(),
    ])
    expect(createResp.status()).toBe(201)

    // New record should appear in the modal list
    await expect(modal.locator('[data-testid="execution-record-row"]')).toHaveCount(1)

    // Close modal and verify the Ejecutado cell amount changed
    await modal.getByTestId('modal-close-btn').click()
    await expect(modal).toBeHidden()

    // Wait for the cell to show the updated amount (retries until timeout)
    await expect(ejecutadoCell).toHaveText(/100/)
  })

  test('deleting a record updates the executed total', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'crud-delete')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Open modal and create a record first
    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await ejecutadoCell.dispatchEvent('dblclick')
    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()

    await modal.locator('[data-testid="entry-type-select"]').selectOption({ label: 'Expense' })
    await modal.locator('[data-testid="amount-input"]').fill('200')
    await modal.locator('#exec-note').fill('E2E test expense')

    const [createResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/executions') && r.request().method() === 'POST'),
      modal.locator('[data-testid="execution-form-submit"]').click(),
    ])
    expect(createResp.status()).toBe(201)
    await expect(modal.locator('[data-testid="execution-record-row"]')).toHaveCount(1)

    // Delete the record
    const [deleteResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/executions/') && r.request().method() === 'DELETE'),
      modal.locator('[data-testid="execution-record-row"]').first().getByTestId('delete-record-btn').click(),
    ])
    expect(deleteResp.status()).toBe(204)

    // Wait for the record to disappear (invalidateAndRefresh completes)
    await expect(modal.locator('[data-testid="execution-record-row"]')).toHaveCount(0)

    // Close modal and verify amount is back to 0 / initial
    await modal.getByTestId('modal-close-btn').click()
    await expect(modal).toBeHidden()

    // Wait for the cell to show 0 (retries until timeout)
    await expect(ejecutadoCell).toHaveText(/0[.,]00/)
  })
})
