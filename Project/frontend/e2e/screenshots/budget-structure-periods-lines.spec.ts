import { test, expect } from '@playwright/test'
import { shoot, flushManifest } from './capture'
import {
  seedOwnerAndLogin,
  expectToast,
  dismissToasts,
  createCycleViaApi,
  createPeriodViaApi,
  createCategoryGroupViaApi,
} from './helpers'

/**
 * Slide screenshots — Budget Structure: Periods (within a cycle) + Budget Lines,
 * including PERIOD_NAME_DUPLICATE / BUDGET_LINE_NAME_DUPLICATE error paths.
 * Images land in docs/slides/budget-structure-periods-lines/.
 */
const FLOW = 'budget-structure-periods-lines'

test.describe('Slides — Budget Structure Periods + Lines', () => {
  test.afterAll(() => flushManifest(FLOW))

  test('period: create → duplicate error → change status → delete', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'slide-periods')
    const cycleId = await createCycleViaApi(page, budgetId, 'E2E Cycle')

    await page.goto(`/budgets/${budgetId}/cycles/${cycleId}`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/cycles/${cycleId}`, { timeout: 10_000 })
    await shoot(page, FLOW, 1, 'period-list-empty', 'Periods — empty list', 'A cycle detail view before any period exists.')

    // --- Create ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Period' }).click()
    await page.getByLabel('Name').fill('January 2024')
    await page.getByLabel('Start Date').fill('2024-01-01')
    await page.getByLabel('End Date').fill('2024-01-31')
    await shoot(page, FLOW, 2, 'period-create-form', 'Create period — form filled', 'The new-period modal filled with a name and date range.')

    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'Period created successfully')
    await shoot(page, FLOW, 3, 'period-create-success', 'Create period — success', 'Success toast and the new period listed.')
    await dismissToasts(page)

    // --- Duplicate name error ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Period' }).click()
    await page.getByLabel('Name').fill('January 2024')
    await page.getByLabel('Start Date').fill('2024-02-01')
    await page.getByLabel('End Date').fill('2024-02-29')
    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'A period with this name already exists in this cycle')
    await shoot(page, FLOW, 4, 'period-create-duplicate-error', 'Create period — duplicate name error', 'Reusing an existing period name in the same cycle is rejected with PERIOD_NAME_DUPLICATE.')
    await dismissToasts(page)
    await page.getByRole('button', { name: 'Cancel' }).click()

    // --- Change status ---
    await page.getByRole('button', { name: 'Change Status' }).first().click()
    await page.getByLabel('Status').selectOption('Closed')
    await shoot(page, FLOW, 5, 'period-status-form', 'Change period status — form', 'The status dropdown set to Closed, ready to save.')

    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'Period status updated')
    await shoot(page, FLOW, 6, 'period-status-success', 'Change period status — success', 'Success toast and the Closed badge on the period.')
    await dismissToasts(page)

    // --- Delete ---
    await page.getByRole('button', { name: 'Delete Period' }).first().click()
    await shoot(page, FLOW, 7, 'period-delete-confirm', 'Delete period — confirm dialog', 'The destructive-action confirmation before a soft-delete.')

    await page.getByRole('button', { name: 'Confirm' }).click()
    await expectToast(page, 'Period deleted successfully')
    await shoot(page, FLOW, 8, 'period-delete-success', 'Delete period — success', 'Period soft-deleted; success toast shown.')
  })

  test('budget line: create → duplicate error → inline edit → delete', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'slide-lines')
    const cycleId = await createCycleViaApi(page, budgetId, 'Lines Cycle')
    await createPeriodViaApi(page, budgetId, cycleId, 'January 2024', 1)
    await createCategoryGroupViaApi(page, budgetId, 'Income Group')

    await page.goto(`/budgets/${budgetId}/lines`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/lines`, { timeout: 10_000 })
    await shoot(page, FLOW, 9, 'line-list-empty', 'Budget Lines — empty list', 'The budget lines view before any line exists.')

    // --- Create ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Line' }).click()
    await expect(page.getByLabel('Name')).toBeVisible({ timeout: 3_000 })
    await page.getByLabel('Name').fill('Salary')
    await page.getByLabel('Type').selectOption('Expense')
    await page.getByLabel('Category Groups').selectOption({ label: 'Income Group' })
    await page.getByLabel('Monthly Amount').fill('5000')
    await page.getByLabel('Start Date').fill('2024-01-01')
    await shoot(page, FLOW, 10, 'line-create-form', 'Create budget line — form filled', 'The new-line modal filled with name, type, group, amount, and start date.')

    const [lineResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/lines') && r.request().method() === 'POST', { timeout: 8_000 }),
      page.getByRole('button', { name: 'Save' }).click(),
    ])
    expect(lineResp.status()).toBe(201)
    await expectToast(page, 'Budget line created successfully')
    await shoot(page, FLOW, 11, 'line-create-success', 'Create budget line — success', 'Success toast and the new line listed.')
    await dismissToasts(page)

    // --- Duplicate name error ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Line' }).click()
    await page.getByLabel('Name').fill('Salary')
    await page.getByLabel('Type').selectOption('Expense')
    await page.getByLabel('Category Groups').selectOption({ label: 'Income Group' })
    await page.getByLabel('Monthly Amount').fill('1000')
    await page.getByLabel('Start Date').fill('2024-01-01')
    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'A budget line with this name already exists in this budget')
    await shoot(page, FLOW, 12, 'line-create-duplicate-error', 'Create budget line — duplicate name error', 'Reusing an existing line name in the same budget is rejected with BUDGET_LINE_NAME_DUPLICATE.')
    await dismissToasts(page)
    await page.getByRole('button', { name: 'Cancel' }).click()

    // --- Inline edit via dblclick ---
    await page.locator('tr').filter({ hasText: 'Salary' }).dblclick()
    const inlineNameInput = page.getByPlaceholder('Name')
    await expect(inlineNameInput).toBeVisible({ timeout: 3_000 })
    await inlineNameInput.clear()
    await inlineNameInput.fill('Monthly Salary')
    await shoot(page, FLOW, 13, 'line-edit-inline', 'Edit budget line — inline', 'Double-click opens inline edit directly in the row (no modal).')

    await page.getByRole('button', { name: 'Save' }).first().click()
    await expectToast(page, 'Budget line updated successfully')
    await shoot(page, FLOW, 14, 'line-edit-success', 'Edit budget line — success', 'Success toast and the updated name reflected in the row.')
    await dismissToasts(page)

    // --- Delete ---
    await page.getByRole('button', { name: 'Delete Line' }).first().click()
    await shoot(page, FLOW, 15, 'line-delete-confirm', 'Delete budget line — confirm dialog', 'The destructive-action confirmation before a soft-delete.')

    await page.getByRole('button', { name: 'Confirm' }).click()
    await expectToast(page, 'Budget line deleted successfully')
    await shoot(page, FLOW, 16, 'line-delete-success', 'Delete budget line — success', 'Soft-deleted line stays listed with a "Deleted" badge.')
  })
})
