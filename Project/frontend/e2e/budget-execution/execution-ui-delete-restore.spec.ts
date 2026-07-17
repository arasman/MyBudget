import { test, expect } from '@playwright/test'
import {
  seedBudgetMatrixFixture,
  goToMatrix,
  createExecutionApi,
  closePeriodApi,
} from '../budget-matrix/helpers'
import { loginWithToken } from '../helpers/auth'
import { expectToast } from '../helpers/toast'

/**
 * E2E: ExecutionRecord delete and restore UI flows.
 *
 * REQ-EXEC-UI-DELETE-1 — SCENARIO-DELETE-2.1 through 2.5
 */
test.describe('ExecutionRecord UI delete and restore', () => {
  /** Helper: open the matrix and the modal for the first Ejecutado cell. */
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

  test('DELETE-2.1: clicking delete enters confirm state without making an API call', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'ui-del-confirm-enter')
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

    // Track whether a DELETE request fires (it should NOT)
    let deleteCallMade = false
    page.on('request', (req) => {
      if (req.url().includes('/executions/') && req.method() === 'DELETE') {
        deleteCallMade = true
      }
    })

    // First click enters confirm state
    await row.getByTestId('delete-record-btn').click()

    // Confirm and cancel buttons appear
    await expect(row.getByTestId('delete-record-confirm-btn')).toBeVisible()
    await expect(row.getByTestId('delete-record-cancel-btn')).toBeVisible()

    // No API call at this point
    expect(deleteCallMade).toBe(false)
  })

  test('DELETE-2.2: cancelling delete resets the row to its original state', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'ui-del-cancel')
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
    await row.getByTestId('delete-record-btn').click()
    await expect(row.getByTestId('delete-record-confirm-btn')).toBeVisible()

    // Track whether a DELETE request fires (it should NOT)
    let deleteCallMade = false
    page.on('request', (req) => {
      if (req.url().includes('/executions/') && req.method() === 'DELETE') {
        deleteCallMade = true
      }
    })

    // Cancel
    await row.getByTestId('delete-record-cancel-btn').click()

    // Row returns to original state
    await expect(row.getByTestId('delete-record-btn')).toBeVisible()
    await expect(row.getByTestId('delete-record-confirm-btn')).not.toBeVisible()

    // No API call was made
    expect(deleteCallMade).toBe(false)
  })

  test('DELETE-2.3: confirming delete shows toast and removes record from list', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'ui-del-confirm')
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
    await row.getByTestId('delete-record-btn').click()
    await expect(row.getByTestId('delete-record-confirm-btn')).toBeVisible()

    // Confirm delete
    const [deleteResp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/executions/') && r.request().method() === 'DELETE',
      ),
      row.getByTestId('delete-record-confirm-btn').click(),
    ])
    expect(deleteResp.status()).toBe(204)

    // Toast fires with delete success message
    await expectToast(page, 'Entry deleted successfully')

    // Record no longer visible in the default (non-deleted) list
    await expect(modal.locator('[data-testid="execution-record-row"]')).toHaveCount(0)
  })

  test('DELETE-2.4: restoring a deleted record shows toast and record reappears in default list', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'ui-restore')
    // Create and then API-delete the record so it is soft-deleted
    const execId = await createExecutionApi(
      request,
      fixture.budgetId,
      fixture.periodIds[0],
      fixture.lineIds[0],
      fixture.accessToken,
    )
    // Soft-delete via API
    await request.delete(
      `/api/budgets/${fixture.budgetId}/periods/${fixture.periodIds[0]}/budget-lines/${fixture.lineIds[0]}/executions/${execId}`,
      { headers: { Authorization: `Bearer ${fixture.accessToken}` } },
    )

    await loginWithToken(page, {
      accessToken: fixture.accessToken,
      activeBudgetId: fixture.budgetId,
    })
    const modal = await openModal(page, fixture.budgetId, fixture.cycleId)

    // Default list shows no records
    await expect(modal.locator('[data-testid="execution-record-row"]')).toHaveCount(0)

    // Toggle include-deleted ON
    await modal.locator('[data-testid="modal-include-deleted-toggle"]').check()

    // Deleted record appears
    const deletedRow = modal.locator('[data-testid="execution-record-row"]').first()
    await expect(deletedRow).toBeVisible()

    // Click restore
    const [restoreResp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/restore') && r.request().method() === 'POST',
      ),
      deletedRow.getByText('Restore').click(),
    ])
    expect(restoreResp.status()).toBe(200)

    // Toast fires with restore success message
    await expectToast(page, 'Entry restored successfully')

    // Toggle include-deleted OFF — record reappears in default list
    await modal.locator('[data-testid="modal-include-deleted-toggle"]').uncheck()
    await expect(modal.locator('[data-testid="execution-record-row"]')).toHaveCount(1)
  })

  test('DELETE-2.5: restore button renders on deleted record in a closed period', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'ui-restore-closed')
    // Seed a record then delete it
    const execId = await createExecutionApi(
      request,
      fixture.budgetId,
      fixture.periodIds[0],
      fixture.lineIds[0],
      fixture.accessToken,
    )
    await request.delete(
      `/api/budgets/${fixture.budgetId}/periods/${fixture.periodIds[0]}/budget-lines/${fixture.lineIds[0]}/executions/${execId}`,
      { headers: { Authorization: `Bearer ${fixture.accessToken}` } },
    )

    // Close the period
    await closePeriodApi(
      request,
      fixture.budgetId,
      fixture.cycleId,
      fixture.periodIds[0],
      fixture.accessToken,
    )

    await loginWithToken(page, {
      accessToken: fixture.accessToken,
      activeBudgetId: fixture.budgetId,
    })
    const modal = await openModal(page, fixture.budgetId, fixture.cycleId)

    // Closed-period banner is visible
    await expect(modal.locator('[data-testid="closed-period-banner"]')).toBeVisible()

    // Toggle include-deleted ON (toggle is visible in list mode only — in closed period it stays in list mode)
    await modal.locator('[data-testid="modal-include-deleted-toggle"]').check()

    // Deleted record row renders
    const deletedRow = modal.locator('[data-testid="execution-record-row"]').first()
    await expect(deletedRow).toBeVisible()

    // Restore button is present (v-else-if branch for closed period + canWrite)
    await expect(deletedRow.getByText('Restore')).toBeVisible()
  })
})
