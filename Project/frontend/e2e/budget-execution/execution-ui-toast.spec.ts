import { test, expect } from '@playwright/test'
import { seedBudgetMatrixFixture, goToMatrix, createExecutionApi } from '../budget-matrix/helpers'
import { loginWithToken } from '../helpers/auth'
import { expectToast } from '../helpers/toast'

/**
 * E2E: Explicit toast assertions for all four ExecutionRecord operations.
 *
 * REQ-EXEC-UI-TOAST-1 — SCENARIO-TOAST-3.1 through 3.4
 */
test.describe('ExecutionRecord UI toasts', () => {
  /** Helper: navigate to matrix and open the modal for the first Ejecutado cell. */
  async function openModal(
    page: Parameters<typeof goToMatrix>[0],
    budgetId: string,
    cycleId: string,
  ) {
    await goToMatrix(page, budgetId, cycleId)
    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await expect(ejecutadoCell).toBeVisible()
    await ejecutadoCell.dispatchEvent('dblclick')
    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()
    return modal
  }

  test('TOAST-3.1: createSuccess toast fires when create API returns 201', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'toast-create')
    await loginWithToken(page, {
      accessToken: fixture.accessToken,
      activeBudgetId: fixture.budgetId,
    })
    const modal = await openModal(page, fixture.budgetId, fixture.cycleId)

    await modal.locator('[data-testid="entry-type-select"]').selectOption({ label: 'Expense' })
    await modal.locator('[data-testid="amount-input"]').fill('100')
    await modal.locator('#exec-note').fill('Toast create test')

    const [createResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/executions') && r.request().method() === 'POST'),
      modal.locator('[data-testid="execution-form-submit"]').click(),
    ])
    expect(createResp.status()).toBe(201)

    await expectToast(page, 'Entry created successfully')
  })

  test('TOAST-3.2: updateSuccess toast fires when update API returns 200', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'toast-update')
    await createExecutionApi(
      request,
      fixture.budgetId,
      fixture.periodIds[0],
      fixture.lineIds[0],
      fixture.accessToken,
      { amount: 100, note: 'original' },
    )

    await loginWithToken(page, {
      accessToken: fixture.accessToken,
      activeBudgetId: fixture.budgetId,
    })
    const modal = await openModal(page, fixture.budgetId, fixture.cycleId)

    // Open edit mode
    const row = modal.locator('[data-testid="execution-record-row"]').first()
    await expect(row).toBeVisible()
    await row.getByText('Edit').click()

    const amountInput = page.locator('[data-testid="amount-input"]')
    await amountInput.clear()
    await amountInput.fill('250')

    const [updateResp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/executions/') && r.request().method() === 'PUT',
      ),
      page.locator('[data-testid="execution-form-submit"]').click(),
    ])
    expect(updateResp.status()).toBe(200)

    await expectToast(page, 'Entry updated successfully')
  })

  test('TOAST-3.3: deleteSuccess toast fires when delete API returns 204', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'toast-delete')
    await createExecutionApi(
      request,
      fixture.budgetId,
      fixture.periodIds[0],
      fixture.lineIds[0],
      fixture.accessToken,
    )

    await loginWithToken(page, {
      accessToken: fixture.accessToken,
      activeBudgetId: fixture.budgetId,
    })
    const modal = await openModal(page, fixture.budgetId, fixture.cycleId)

    const row = modal.locator('[data-testid="execution-record-row"]').first()
    await expect(row).toBeVisible()
    await row.getByTestId('delete-record-btn').click()
    await expect(row.getByTestId('delete-record-confirm-btn')).toBeVisible()

    const [deleteResp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/executions/') && r.request().method() === 'DELETE',
      ),
      row.getByTestId('delete-record-confirm-btn').click(),
    ])
    expect(deleteResp.status()).toBe(204)

    await expectToast(page, 'Entry deleted successfully')
  })

  test('TOAST-3.4: restoreSuccess toast fires when restore API returns 200', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'toast-restore')
    const execId = await createExecutionApi(
      request,
      fixture.budgetId,
      fixture.periodIds[0],
      fixture.lineIds[0],
      fixture.accessToken,
    )
    // Soft-delete the record via API
    await request.delete(
      `/api/budgets/${fixture.budgetId}/periods/${fixture.periodIds[0]}/budget-lines/${fixture.lineIds[0]}/executions/${execId}`,
      { headers: { Authorization: `Bearer ${fixture.accessToken}` } },
    )

    await loginWithToken(page, {
      accessToken: fixture.accessToken,
      activeBudgetId: fixture.budgetId,
    })
    const modal = await openModal(page, fixture.budgetId, fixture.cycleId)

    // Show deleted records
    await modal.locator('[data-testid="modal-include-deleted-toggle"]').check()

    const deletedRow = modal.locator('[data-testid="execution-record-row"]').first()
    await expect(deletedRow).toBeVisible()

    const [restoreResp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/restore') && r.request().method() === 'POST',
      ),
      deletedRow.getByText('Restore').click(),
    ])
    expect(restoreResp.status()).toBe(200)

    await expectToast(page, 'Entry restored successfully')
  })
})
