import { test, expect } from '@playwright/test'
import { seedOwnerAndLogin, seedDeletedBudgetLine, expectToast } from './helpers'

/**
 * E2E: Budget Lines CRUD — create → inline edit via dblclick → delete
 *
 * Prerequisites: Docker Compose stack running.
 * Creates a cycle + period via API to navigate directly to the lines view.
 * dblclick → inline edit mode (inputs in row, Check/X icons); Pencil → modal.
 */
test.describe('Budget Structure — Budget Lines', () => {
  test.describe('soft-delete / restore', () => {
    /** Shared setup: registers user, creates cycle + period via API. */
    async function setupPeriod(page: Parameters<typeof seedOwnerAndLogin>[0]) {
      const { budgetId } = await seedOwnerAndLogin(page, 'lines-sd')
      const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')
      const headers = { Authorization: `Bearer ${token}` }

      const cycleResp = await page.request.post(`/api/budgets/${budgetId}/cycles`, {
        data: {
          name: 'SD Lines Cycle',
          startDate: '2024-01-01',
          endDate: '2024-12-31',
          defaultCurrencyId: '11111111-1111-1111-1111-111111111111',
        },
        headers,
      })
      expect(cycleResp.status()).toBe(201)
      const { id: cycleId } = await cycleResp.json()

      const periodResp = await page.request.post(
        `/api/budgets/${budgetId}/cycles/${cycleId}/periods`,
        {
          data: {
            name: 'SD Period',
            periodNumber: 1,
            startDate: '2024-01-01',
            endDate: '2024-01-31',
          },
          headers,
        },
      )
      expect(periodResp.status()).toBe(201)
      const { id: periodId } = await periodResp.json()

      return { budgetId, cycleId, periodId, token }
    }

    test('toggle ON reveals deleted budget line', async ({ page }) => {
      const { budgetId, cycleId, periodId, token } = await setupPeriod(page)
      const deletedLineId = await seedDeletedBudgetLine(page, budgetId, periodId, token)

      await page.goto(`/budgets/${budgetId}/lines`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/lines`, { timeout: 10_000 })

      // Wait for the view to mount before asserting absence
      await expect(page.getByLabel('Show deleted')).toBeVisible({ timeout: 10_000 })

      // Deleted line must NOT be visible with toggle OFF (default)
      await expect(page.getByText('Deleted Line').first()).not.toBeVisible({ timeout: 5_000 })

      // Toggle ON
      await page.getByLabel('Show deleted').check()

      await expect(page.getByRole('row').filter({ hasText: 'Deleted Line' }).first()).toBeVisible({ timeout: 5_000 })
    })

    test('toggle OFF hides deleted budget line', async ({ page }) => {
      const { budgetId, cycleId, periodId, token } = await setupPeriod(page)
      await seedDeletedBudgetLine(page, budgetId, periodId, token)

      await page.goto(`/budgets/${budgetId}/lines`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/lines`, { timeout: 10_000 })

      // Toggle ON first
      await page.getByLabel('Show deleted').check()
      await expect(page.getByText('Deleted Line').first()).toBeVisible({ timeout: 5_000 })

      // Toggle OFF — deleted line must disappear
      await page.getByLabel('Show deleted').uncheck()
      await expect(page.getByText('Deleted Line').first()).not.toBeVisible({ timeout: 5_000 })
    })

    test('restore returns budget line to active list with success toast', async ({ page }) => {
      const { budgetId, cycleId, periodId, token } = await setupPeriod(page)
      await seedDeletedBudgetLine(page, budgetId, periodId, token)

      await page.goto(`/budgets/${budgetId}/lines`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/lines`, { timeout: 10_000 })

      // Toggle ON to reveal deleted line
      await page.getByLabel('Show deleted').check()
      await expect(page.getByText('Deleted Line').first()).toBeVisible({ timeout: 5_000 })

      // Click Restore on the deleted line
      await page.getByRole('button', { name: 'Restore' }).first().click()

      await expectToast(page, 'Budget line restored successfully')

      // Toggle OFF — restored line must appear in active list
      await page.getByLabel('Show deleted').uncheck()
      await expect(page.getByText('Deleted Line').first()).toBeVisible({ timeout: 5_000 })
    })
  })

  test('create line → edit via dblclick → delete', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'lines')

    const token = await page.evaluate(() => localStorage.getItem('accessToken'))
    const headers = { Authorization: `Bearer ${token}` }

    // Create cycle via API
    const cycleResp = await page.request.post(`/api/budgets/${budgetId}/cycles`, {
      data: { name: 'Lines Cycle', startDate: '2024-01-01', endDate: '2024-12-31', defaultCurrencyId: '11111111-1111-1111-1111-111111111111' },
      headers,
    })
    expect(cycleResp.status()).toBe(201)
    const { id: cycleId } = await cycleResp.json()

    // Create period via API
    const periodResp = await page.request.post(
      `/api/budgets/${budgetId}/cycles/${cycleId}/periods`,
      {
        data: {
          name: 'January 2024',
          periodNumber: 1,
          startDate: '2024-01-01',
          endDate: '2024-01-31',
        },
        headers,
      },
    )
    expect(periodResp.status()).toBe(201)
    const { id: periodId } = await periodResp.json()

    // Create category group via API (required by backend for budget lines)
    const groupResp = await page.request.post(`/api/budgets/${budgetId}/category-groups`, {
      data: { name: 'Income Group', displayOrder: 1 },
      headers,
    })
    expect(groupResp.status()).toBe(201)

    // Navigate to budget lines
    await page.goto(`/budgets/${budgetId}/lines`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/lines`, { timeout: 10_000 })

    // --- Create budget line ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Line' }).click()

    await expect(page.getByLabel('Name')).toBeVisible({ timeout: 3_000 })
    await page.getByLabel('Name').fill('Salary')
    // Select line type
    const lineTypeSelect = page.getByLabel('Type')
    await lineTypeSelect.selectOption('Expense')
    // Select category group (required)
    await page.getByLabel('Category Groups').selectOption({ label: 'Income Group' })
    // Set budgeted amount
    const amountInput = page.getByLabel('Monthly Amount')
    await amountInput.fill('5000')
    // Set start date (required by BudgetLine redesign)
    await page.getByLabel('Start Date').fill('2024-01-01')

    const [lineResp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/lines') && r.request().method() === 'POST',
        { timeout: 8_000 },
      ),
      page.getByRole('button', { name: 'Save' }).click(),
    ])
    expect(lineResp.status()).toBe(201)

    await expectToast(page, 'Budget line created successfully')

    await expect(page.getByText('Salary')).toBeVisible({ timeout: 5_000 })

    // --- Edit via dblclick (inline edit — no modal) ---
    await page.locator('tr').filter({ hasText: 'Salary' }).dblclick()

    // Inline edit mode: name input appears directly in the row
    const inlineNameInput = page.getByPlaceholder('Name')
    await expect(inlineNameInput).toBeVisible({ timeout: 3_000 })
    await inlineNameInput.clear()
    await inlineNameInput.fill('Monthly Salary')
    // Save via the Check icon button (title="Save")
    await page.getByRole('button', { name: 'Save' }).first().click()

    await expectToast(page, 'Budget line updated successfully')

    await expect(page.getByText('Monthly Salary')).toBeVisible({ timeout: 5_000 })

    // --- Delete line ---
    // Use the page action "New Line" button or inline delete
    // The BudgetLineRow emits delete — find the delete button
    await page.getByRole('button', { name: 'Delete Line' }).first().click()
    const [deleteResp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/lines/') && r.request().method() === 'DELETE',
        { timeout: 8_000 },
      ),
      page.getByRole('button', { name: 'Confirm' }).click(),
    ])
    expect(deleteResp.status()).toBe(204)

    await expectToast(page, 'Budget line deleted successfully')

    // Soft-delete keeps the row visible with a "Deleted" badge (opacity-60) — assert that
    await expect(
      page.getByRole('row', { name: /Monthly Salary/i }).getByText('Deleted'),
    ).toBeVisible({ timeout: 5_000 })
  })
})
