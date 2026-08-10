import { test, expect } from '@playwright/test'
import { shoot, flushManifest } from './capture'
import { seedBudgetMatrixFixture, loginWithToken, goToMatrix } from '../budget-matrix/helpers'

/**
 * Slide screenshots — Budget Execution (the matrix): opening the execution
 * modal, the note-required validation error, create success, currency
 * toggle, group collapse, and delete. Reuses budget-matrix/helpers.ts (API
 * fixture seeding) rather than duplicating it.
 * Images land in docs/slides/budget-execution/.
 */
const FLOW = 'budget-execution'

test.describe('Slides — Budget Execution', () => {
  test.afterAll(() => flushManifest(FLOW))

  test('matrix → create (validation error → success) → currency toggle → collapse → delete', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'slide-matrix')
    await loginWithToken(page, fixture.accessToken, fixture.budgetId)
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await expect(ejecutadoCell).toBeVisible()
    await shoot(page, FLOW, 1, 'matrix-view', 'Budget Execution — matrix view', 'Budget lines by category group, periods as columns, before any execution is recorded.')

    // --- Open execution modal ---
    await ejecutadoCell.dispatchEvent('dblclick')
    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()
    await shoot(page, FLOW, 2, 'open-execution-modal', 'Open execution modal', 'Double-clicking an Ejecutado cell opens the execution list/create modal for that line and period.')

    // --- Validation error: note is required for every entry type ---
    await modal.locator('[data-testid="entry-type-select"]').selectOption({ label: 'Credit Note' })
    await modal.locator('[data-testid="amount-input"]').fill('50')
    await modal.locator('[data-testid="execution-form-submit"]').click()
    await expect(modal.locator('[data-testid="note-error"]')).toBeVisible()
    await shoot(page, FLOW, 3, 'create-validation-error', 'Create execution — note required error', 'Submitting without a note is rejected client-side — note is mandatory for every entry type.')

    // --- Fill and submit successfully ---
    await modal.locator('[data-testid="entry-type-select"]').selectOption({ label: 'Expense' })
    await modal.locator('[data-testid="amount-input"]').fill('100')
    await modal.locator('#exec-note').fill('E2E slide expense')
    await shoot(page, FLOW, 4, 'create-form-filled', 'Create execution — form filled', 'Expense entry with amount and note, ready to submit.')

    const [createResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/executions') && r.request().method() === 'POST'),
      modal.locator('[data-testid="execution-form-submit"]').click(),
    ])
    expect(createResp.status()).toBe(201)
    await expect(modal.locator('[data-testid="execution-record-row"]')).toHaveCount(1)
    await shoot(page, FLOW, 5, 'create-success', 'Create execution — success', 'The new record appears in the modal list.')

    await modal.getByTestId('modal-close-btn').click()
    await expect(modal).toBeHidden()
    await expect(ejecutadoCell).toHaveText(/100/)
    await shoot(page, FLOW, 6, 'matrix-updated', 'Matrix — updated Ejecutado total', 'The Ejecutado cell reflects the new execution total after closing the modal.')

    // --- Currency toggle ---
    await page.getByTestId('currency-usd-btn').click()
    await page.waitForTimeout(300)
    await shoot(page, FLOW, 7, 'currency-toggle-usd', 'Currency toggle — USD', "Amounts convert to USD at the cycle's exchange rate.")

    await page.getByTestId('currency-gtq-btn').click()
    await page.waitForTimeout(300)
    await shoot(page, FLOW, 8, 'currency-toggle-gtq', 'Currency toggle — back to GTQ', 'Toggling back restores the original GTQ amounts.')

    // --- Collapse a group ---
    const groupRow = page.locator('[data-testid="matrix-group-row"]').first()
    const categoryRow = page.locator('[data-testid="matrix-category-row"]').first()
    await groupRow.getByTestId('group-collapse-btn').click()
    await expect(categoryRow).toBeHidden()
    await shoot(page, FLOW, 9, 'collapse-group', 'Collapse group', 'Collapsing a category group hides its category rows for a denser overview.')
    await groupRow.getByTestId('group-collapse-btn').click()
    await expect(categoryRow).toBeVisible()

    // --- Delete the execution ---
    await ejecutadoCell.dispatchEvent('dblclick')
    await expect(modal).toBeVisible()
    await modal.locator('[data-testid="execution-record-row"]').first().getByTestId('delete-record-btn').click()
    await shoot(page, FLOW, 10, 'delete-confirm', 'Delete execution — confirm', 'A first click arms a two-step confirm before the destructive delete.')

    const [deleteResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/executions/') && r.request().method() === 'DELETE'),
      modal.locator('[data-testid="execution-record-row"]').first().getByTestId('delete-record-confirm-btn').click(),
    ])
    expect(deleteResp.status()).toBe(204)
    await expect(modal.locator('[data-testid="execution-record-row"]')).toHaveCount(0)
    await shoot(page, FLOW, 11, 'delete-success', 'Delete execution — success', 'The record list is empty again after delete.')
  })
})
