import { test, expect } from '@playwright/test'
import { seedOwnerAndLogin } from './helpers'

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

    // Verify cycle appears in the list
    await expect(page.getByText('Test Cycle 2024')).toBeVisible({ timeout: 5_000 })

    // --- Edit cycle ---
    await page.getByRole('button', { name: 'Edit Cycle' }).first().click()
    await page.getByLabel('Name').clear()
    await page.getByLabel('Name').fill('Updated Cycle 2024')
    await page.getByRole('button', { name: 'Save' }).click()

    await expect(page.getByText('Updated Cycle 2024')).toBeVisible({ timeout: 5_000 })

    // --- Set active ---
    const setActiveBtn = page.getByRole('button', { name: 'Set as Active' }).first()
    // Button may be disabled if already active; only click when enabled
    if (await setActiveBtn.isEnabled()) {
      await setActiveBtn.click()
      // After setting active, the Active badge should be visible
      await expect(page.getByText('Active').first()).toBeVisible({ timeout: 5_000 })
    }

    // --- Delete cycle ---
    await page.getByRole('button', { name: 'Delete Cycle' }).first().click()
    // Confirm the delete dialog
    await page.getByRole('button', { name: 'Confirm' }).click()

    // Verify cycle removed
    await expect(page.getByText('Updated Cycle 2024')).not.toBeVisible({ timeout: 5_000 })
  })
})
