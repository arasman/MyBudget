import { test, expect } from '@playwright/test'
import { seedBudgetMatrixFixture, loginWithToken, goToMatrix } from './helpers'

/**
 * E2E: BudgetMatrix execution form note validation.
 * Note is always required for all entry types.
 *
 * REQ-MATRIX-EXEC
 */
test.describe('BudgetMatrix note validation', () => {
  test('Credit Note without note shows validation error', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'note-credit')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Open modal
    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await ejecutadoCell.dispatchEvent('dblclick')
    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()

    // Select Credit Note entry type
    await modal.locator('[data-testid="entry-type-select"]').selectOption({ label: 'Credit Note' })
    await modal.locator('[data-testid="amount-input"]').fill('50')
    // Leave note field empty

    // Submit without filling the note
    await modal.locator('[data-testid="execution-form-submit"]').click()

    // Validation error should appear; no API call should have been made
    const errorMsg = modal.locator('[data-testid="note-error"]')
    await expect(errorMsg).toBeVisible()
  })

  test('Expense with note submits successfully', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'note-expense')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Open modal
    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await ejecutadoCell.dispatchEvent('dblclick')
    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()

    // Select Expense — note is required for all entry types
    await modal.locator('[data-testid="entry-type-select"]').selectOption({ label: 'Expense' })
    await modal.locator('[data-testid="amount-input"]').fill('50')
    await modal.locator('#exec-note').fill('E2E expense note')

    const [createResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/executions') && r.request().method() === 'POST'),
      modal.locator('[data-testid="execution-form-submit"]').click(),
    ])
    expect(createResp.status()).toBe(201)

    // No validation error visible
    await expect(modal.locator('[data-testid="note-error"]')).toHaveCount(0)
    // Record appears in list
    await expect(modal.locator('[data-testid="execution-record-row"]')).toHaveCount(1)
  })
})
