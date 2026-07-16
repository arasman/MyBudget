import { test, expect } from '@playwright/test'
import { seedOwnerAndLogin } from './helpers'

/**
 * E2E: Budget Lines CRUD — create → inline edit via dblclick → delete
 *
 * Prerequisites: Docker Compose stack running.
 * Creates a cycle + period via API to navigate directly to the lines view.
 * dblclick → inline edit mode (inputs in row, Check/X icons); Pencil → modal.
 */
test.describe('Budget Structure — Budget Lines', () => {
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
    await page.goto(`/budgets/${budgetId}/cycles/${cycleId}/periods/${periodId}/lines`)
    await expect(page).toHaveURL(
      `/budgets/${budgetId}/cycles/${cycleId}/periods/${periodId}/lines`,
      { timeout: 10_000 },
    )

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
    const amountInput = page.getByLabel('Budgeted Amount')
    await amountInput.fill('5000')

    const [lineResp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/lines') && r.request().method() === 'POST',
        { timeout: 8_000 },
      ),
      page.getByRole('button', { name: 'Save' }).click(),
    ])
    expect(lineResp.status()).toBe(201)

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

    await expect(page.getByText('Monthly Salary')).toBeVisible({ timeout: 5_000 })

    // --- Delete line ---
    // Use the page action "New Line" button or inline delete
    // The BudgetLineRow emits delete — find the delete button
    await page.getByRole('button', { name: 'Delete Line' }).first().click()
    await page.getByRole('button', { name: 'Confirm' }).click()

    await expect(page.getByText('Monthly Salary')).not.toBeVisible({ timeout: 5_000 })
  })
})
