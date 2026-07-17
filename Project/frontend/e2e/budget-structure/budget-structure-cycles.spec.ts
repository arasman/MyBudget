import { test, expect } from '@playwright/test'
import { seedOwnerAndLogin, seedDeletedCycle, expectToast } from './helpers'

/**
 * E2E: Full Cycle CRUD flow
 *
 * Prerequisites: Docker Compose stack running.
 * User is automatically an Owner (full admin) of their auto-created budget.
 */
test.describe('Budget Structure — Cycles CRUD', () => {
  test('create cycle → edit → set active → delete', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'cycles')

    // Navigate to the cycles list
    await page.goto(`/budgets/${budgetId}/cycles`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/cycles`, { timeout: 10_000 })

    // --- Create cycle ---
    // Click "New Cycle" page action button in the navbar
    await page.getByRole('navigation').getByRole('button', { name: 'New Cycle' }).click()

    // Fill in the cycle form modal
    await page.getByLabel('Name').fill('Test Cycle 2024')
    await page.getByLabel('Start Date').fill('2024-01-01')
    await page.getByLabel('End Date').fill('2024-12-31')
    await page.getByRole('button', { name: 'Save' }).click()

    await expectToast(page, 'Cycle created successfully')

    // Verify cycle appears in the list
    await expect(page.getByText('Test Cycle 2024')).toBeVisible({ timeout: 5_000 })

    // --- Edit cycle ---
    await page.getByRole('button', { name: 'Edit Cycle' }).first().click()
    await page.getByLabel('Name').clear()
    await page.getByLabel('Name').fill('Updated Cycle 2024')
    await page.getByRole('button', { name: 'Save' }).click()

    await expectToast(page, 'Cycle updated successfully')

    await expect(page.getByText('Updated Cycle 2024')).toBeVisible({ timeout: 5_000 })

    // --- Set active ---
    const setActiveBtn = page.getByRole('button', { name: 'Set as Active' }).first()
    // Button may be disabled if already active; only click when enabled
    if (await setActiveBtn.isEnabled()) {
      await setActiveBtn.click()
      await expectToast(page, 'Cycle set as active')
      // After setting active, the Active badge should be visible
      await expect(page.getByText('Active').first()).toBeVisible({ timeout: 5_000 })
    }

    // --- Delete cycle ---
    await page.getByRole('button', { name: 'Delete Cycle' }).first().click()
    // Confirm the delete dialog
    await page.getByRole('button', { name: 'Confirm' }).click()

    await expectToast(page, 'Cycle deleted successfully')

    // Verify cycle removed
    await expect(page.getByText('Updated Cycle 2024')).not.toBeVisible({ timeout: 5_000 })
  })

  test.describe('soft-delete / restore', () => {
    test('toggle ON reveals deleted cycle', async ({ page }) => {
      const { budgetId } = await seedOwnerAndLogin(page, 'cycles-sd-toggle')
      const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')

      const deletedCycleId = await seedDeletedCycle(page, budgetId, token)

      await page.goto(`/budgets/${budgetId}/cycles`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/cycles`, { timeout: 10_000 })

      // Wait for the list container to mount before asserting absence
      await expect(page.getByRole('table')).toBeVisible({ timeout: 10_000 })

      // Deleted cycle must NOT be visible with toggle OFF (default)
      await expect(page.getByText(`Deleted Cycle`).first()).not.toBeVisible({ timeout: 5_000 })

      // Toggle ON — show deleted
      await page.getByLabel('Show deleted').check()

      // Deleted cycle must appear after toggle ON
      await expect(page.getByRole('row').filter({ hasText: 'Deleted Cycle' }).first()).toBeVisible({ timeout: 5_000 })
    })

    test('toggle OFF hides deleted cycle', async ({ page }) => {
      const { budgetId } = await seedOwnerAndLogin(page, 'cycles-sd-off')
      const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')

      await seedDeletedCycle(page, budgetId, token)

      await page.goto(`/budgets/${budgetId}/cycles`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/cycles`, { timeout: 10_000 })

      // Toggle ON first to make deleted item visible
      await page.getByLabel('Show deleted').check()
      await expect(page.getByText('Deleted Cycle').first()).toBeVisible({ timeout: 5_000 })

      // Toggle OFF — deleted cycle must disappear
      await page.getByLabel('Show deleted').uncheck()
      await expect(page.getByText('Deleted Cycle').first()).not.toBeVisible({ timeout: 5_000 })
    })

    test('restore returns cycle to active list with success toast', async ({ page }) => {
      const { budgetId } = await seedOwnerAndLogin(page, 'cycles-restore')
      const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')

      await seedDeletedCycle(page, budgetId, token)

      await page.goto(`/budgets/${budgetId}/cycles`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/cycles`, { timeout: 10_000 })

      // Toggle ON to reveal deleted cycle
      await page.getByLabel('Show deleted').check()
      const deletedRow = page.getByText('Deleted Cycle').first()
      await expect(deletedRow).toBeVisible({ timeout: 5_000 })

      // Click Restore on the deleted cycle row
      await page.getByRole('button', { name: 'Restore' }).first().click()

      await expectToast(page, 'Cycle restored successfully')

      // Toggle OFF — restored cycle must now appear in the active list
      await page.getByLabel('Show deleted').uncheck()
      await expect(page.getByText('Deleted Cycle').first()).toBeVisible({ timeout: 5_000 })
    })
  })

  test('create cycle with alternate currency → list shows USD → detail shows exchange rate', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'cycles-alt')

    await page.goto(`/budgets/${budgetId}/cycles`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/cycles`, { timeout: 10_000 })

    // Open create form
    await page.getByRole('navigation').getByRole('button', { name: 'New Cycle' }).click()

    await page.getByLabel('Name').fill('Alt Currency Cycle')
    await page.getByLabel('Start Date').fill('2025-01-01')
    await page.getByLabel('End Date').fill('2025-12-31')

    // Wait for currencies to load, then select alternate currency (USD)
    await expect(page.getByLabel('Alternate Currency').locator('option', { hasText: 'US Dollar' })).toBeAttached({ timeout: 5_000 })
    await page.getByLabel('Alternate Currency').selectOption({ label: '$ US Dollar (USD)' })

    // Exchange rate input should appear; fill it
    await expect(page.getByLabel(/per 1/)).toBeVisible({ timeout: 3_000 })
    await page.locator('input[type="number"]').fill('7.5')

    await page.getByRole('button', { name: 'Save' }).click()

    // Verify cycle appears in the list with alternate currency column
    await expect(page.getByText('Alt Currency Cycle')).toBeVisible({ timeout: 5_000 })
    await expect(page.getByText(/\$ USD/)).toBeVisible({ timeout: 5_000 })

    // Navigate to cycle detail
    await page.getByRole('button', { name: 'View Periods' }).first().click()

    // Verify exchange rate info is shown in detail view
    await expect(page.getByText(/7\.5 GTQ = 1 USD/i)).toBeVisible({ timeout: 5_000 })
  })
})
