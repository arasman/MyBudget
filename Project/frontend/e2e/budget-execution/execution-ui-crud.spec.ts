import { test, expect } from '@playwright/test'
import { seedBudgetMatrixFixture, goToMatrix, createExecutionApi } from '../budget-matrix/helpers'
import { loginWithToken } from '../helpers/auth'
import { expectToast } from '../helpers/toast'

/**
 * E2E: ExecutionRecord CRUD UI flows through ExecutionListModal.
 *
 * REQ-EXEC-UI-CRUD-1 — SCENARIO-CRUD-1.1 through 1.4
 */
test.describe('ExecutionRecord UI CRUD', () => {
  test('CRUD-1.1: creating a record shows toast and record appears in list', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'ui-crud-create')
    await loginWithToken(page, {
      accessToken: fixture.accessToken,
      activeBudgetId: fixture.budgetId,
    })
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    // Open modal via double-click on Ejecutado cell
    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await expect(ejecutadoCell).toBeVisible()
    await ejecutadoCell.dispatchEvent('dblclick')

    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()

    // Fill the create form
    await modal.locator('[data-testid="entry-type-select"]').selectOption({ label: 'Expense' })
    await modal.locator('[data-testid="amount-input"]').fill('150')
    await modal.locator('#exec-note').fill('UI CRUD create test')

    // Submit and wait for the API response
    const [createResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/executions') && r.request().method() === 'POST'),
      modal.locator('[data-testid="execution-form-submit"]').click(),
    ])
    expect(createResp.status()).toBe(201)

    // Toast fires with create success message
    await expectToast(page, 'Entry created successfully')

    // Record appears in modal list
    await expect(modal.locator('[data-testid="execution-record-row"]')).toHaveCount(1)
  })

  test('CRUD-1.2: create form OperationDate defaults to today', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'ui-crud-date')
    await loginWithToken(page, {
      accessToken: fixture.accessToken,
      activeBudgetId: fixture.budgetId,
    })
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await expect(ejecutadoCell).toBeVisible()
    await ejecutadoCell.dispatchEvent('dblclick')

    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()

    // Verify the operation date field defaults to today
    const today = new Date()
    const todayIso = [
      today.getFullYear(),
      String(today.getMonth() + 1).padStart(2, '0'),
      String(today.getDate()).padStart(2, '0'),
    ].join('-')

    const dateInput = modal.locator('#exec-operation-date')
    await expect(dateInput).toHaveValue(todayIso)
  })

  test('CRUD-1.3: updating a record shows toast and reflects change in list', async ({
    page,
    request,
  }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'ui-crud-update')
    // Seed an existing record via API
    await createExecutionApi(request, fixture.budgetId, fixture.periodIds[0], fixture.lineIds[0], fixture.accessToken, {
      amount: 200,
      note: 'original note',
    })

    await loginWithToken(page, {
      accessToken: fixture.accessToken,
      activeBudgetId: fixture.budgetId,
    })
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await expect(ejecutadoCell).toBeVisible()
    await ejecutadoCell.dispatchEvent('dblclick')

    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()

    // Click edit on the existing record row
    const row = modal.locator('[data-testid="execution-record-row"]').first()
    await expect(row).toBeVisible()
    await row.getByText('Edit').click()

    // Modify the amount
    const amountInput = page.locator('[data-testid="amount-input"]')
    await amountInput.clear()
    await amountInput.fill('350')

    // Submit and wait for PUT/PATCH response
    const [updateResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/executions/') && r.request().method() === 'PUT'),
      page.locator('[data-testid="execution-form-submit"]').click(),
    ])
    expect(updateResp.status()).toBe(200)

    // Toast fires with update success message
    await expectToast(page, 'Entry updated successfully')

    // Updated amount visible in the modal list
    await expect(modal.locator('[data-testid="execution-record-row"]').first()).toContainText('350')
  })

  test('CRUD-1.4: edit form pre-fills existing record values', async ({ page, request }) => {
    const fixture = await seedBudgetMatrixFixture(request, 'ui-crud-prefill')
    // Seed a record with known amount and entry type
    await createExecutionApi(request, fixture.budgetId, fixture.periodIds[0], fixture.lineIds[0], fixture.accessToken, {
      entryType: 1,
      amount: 500,
      note: 'prefill check note',
    })

    await loginWithToken(page, {
      accessToken: fixture.accessToken,
      activeBudgetId: fixture.budgetId,
    })
    await goToMatrix(page, fixture.budgetId, fixture.cycleId)

    const ejecutadoCell = page.locator('[data-testid="matrix-cell-ejecutado"]').first()
    await expect(ejecutadoCell).toBeVisible()
    await ejecutadoCell.dispatchEvent('dblclick')

    const modal = page.locator('[data-testid="execution-list-modal"]')
    await expect(modal).toBeVisible()

    // Click edit on the existing record row
    const row = modal.locator('[data-testid="execution-record-row"]').first()
    await expect(row).toBeVisible()
    await row.getByText('Edit').click()

    // Verify pre-filled values
    await expect(page.locator('[data-testid="amount-input"]')).toHaveValue('500')
    await expect(page.locator('[data-testid="entry-type-select"]')).toHaveValue('1')
  })
})
