import { test, expect } from '@playwright/test'
import { seedOwnerAndLogin, seedDeletedPeriod, expectToast } from './helpers'

/**
 * E2E: Period management within a cycle
 *
 * Prerequisites: Docker Compose stack running.
 */
test.describe('Budget Structure — Period Management', () => {
  test.describe('soft-delete / restore', () => {
    /** Shared setup: registers a fresh user and creates a cycle via API. */
    async function setupCycle(page: Parameters<typeof seedOwnerAndLogin>[0]) {
      const { budgetId } = await seedOwnerAndLogin(page, 'periods-sd')
      const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')
      const cycleResp = await page.request.post(`/api/budgets/${budgetId}/cycles`, {
        data: {
          name: 'SD Cycle',
          startDate: '2024-01-01',
          endDate: '2024-12-31',
          defaultCurrencyId: '11111111-1111-1111-1111-111111111111',
        },
        headers: { Authorization: `Bearer ${token}` },
      })
      expect(cycleResp.status()).toBe(201)
      const { id: cycleId } = await cycleResp.json()
      return { budgetId, cycleId, token }
    }

    test('toggle ON reveals deleted period', async ({ page }) => {
      const { budgetId, cycleId, token } = await setupCycle(page)
      const deletedPeriodId = await seedDeletedPeriod(page, budgetId, cycleId, token)

      await page.goto(`/budgets/${budgetId}/cycles/${cycleId}`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/cycles/${cycleId}`, { timeout: 10_000 })

      // Wait for the list container to mount before asserting absence
      await expect(page.getByRole('table')).toBeVisible({ timeout: 10_000 })

      // Deleted period must NOT be visible with toggle OFF (default)
      await expect(page.getByText('Deleted Period').first()).not.toBeVisible({ timeout: 5_000 })

      // Toggle ON
      await page.getByLabel('Show deleted').check()

      await expect(page.getByRole('row').filter({ hasText: 'Deleted Period' }).first()).toBeVisible({ timeout: 5_000 })
    })

    test('toggle OFF hides deleted period', async ({ page }) => {
      const { budgetId, cycleId, token } = await setupCycle(page)
      await seedDeletedPeriod(page, budgetId, cycleId, token)

      await page.goto(`/budgets/${budgetId}/cycles/${cycleId}`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/cycles/${cycleId}`, { timeout: 10_000 })

      // Toggle ON first
      await page.getByLabel('Show deleted').check()
      await expect(page.getByText('Deleted Period').first()).toBeVisible({ timeout: 5_000 })

      // Toggle OFF — deleted period must disappear
      await page.getByLabel('Show deleted').uncheck()
      await expect(page.getByText('Deleted Period').first()).not.toBeVisible({ timeout: 5_000 })
    })

    test('restore — confirm path restores period and shows toast', async ({ page }) => {
      const { budgetId, cycleId, token } = await setupCycle(page)
      await seedDeletedPeriod(page, budgetId, cycleId, token)

      await page.goto(`/budgets/${budgetId}/cycles/${cycleId}`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/cycles/${cycleId}`, { timeout: 10_000 })

      // Toggle ON to reveal deleted period
      await page.getByLabel('Show deleted').check()
      await expect(page.getByText('Deleted Period').first()).toBeVisible({ timeout: 5_000 })

      // Click Restore — cascade disclosure dialog appears
      await page.getByRole('button', { name: 'Restore' }).first().click()

      // Confirm path
      await page.getByRole('button', { name: 'Confirm' }).click()

      await expectToast(page, 'Period restored successfully')

      // Toggle OFF — restored period must appear in active list
      await page.getByLabel('Show deleted').uncheck()
      await expect(page.getByText('Deleted Period').first()).toBeVisible({ timeout: 5_000 })
    })

    test('restore — cancel path aborts restore', async ({ page }) => {
      const { budgetId, cycleId, token } = await setupCycle(page)
      await seedDeletedPeriod(page, budgetId, cycleId, token)

      await page.goto(`/budgets/${budgetId}/cycles/${cycleId}`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/cycles/${cycleId}`, { timeout: 10_000 })

      // Toggle ON
      await page.getByLabel('Show deleted').check()
      await expect(page.getByText('Deleted Period').first()).toBeVisible({ timeout: 5_000 })

      // Click Restore — cascade disclosure dialog appears
      await page.getByRole('button', { name: 'Restore' }).first().click()

      // Cancel path — dialog dismissed, no restore occurs
      await page.getByRole('button', { name: 'Cancel' }).click()

      // Period must remain absent from active list (toggle OFF)
      await page.getByLabel('Show deleted').uncheck()
      await expect(page.getByText('Deleted Period').first()).not.toBeVisible({ timeout: 5_000 })
    })
  })

  test('create period → change status → delete', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'periods')

    // First create a cycle via API (faster than UI for setup)
    const cycleResp = await page.request.post(`/api/budgets/${budgetId}/cycles`, {
      data: { name: 'E2E Cycle', startDate: '2024-01-01', endDate: '2024-12-31', defaultCurrencyId: '11111111-1111-1111-1111-111111111111' },
      headers: {
        Authorization: `Bearer ${await page.evaluate(() => localStorage.getItem('accessToken'))}`,
      },
    })
    expect(cycleResp.status()).toBe(201)
    const { id: cycleId } = await cycleResp.json()

    // Navigate to cycle detail
    await page.goto(`/budgets/${budgetId}/cycles/${cycleId}`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/cycles/${cycleId}`, { timeout: 10_000 })

    // --- Create period ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Period' }).click()

    await page.getByLabel('Name').fill('January 2024')
    await page.getByLabel('Start Date').fill('2024-01-01')
    await page.getByLabel('End Date').fill('2024-01-31')
    await page.getByRole('button', { name: 'Save' }).click()

    await expectToast(page, 'Period created successfully')

    await expect(page.getByText('January 2024')).toBeVisible({ timeout: 5_000 })

    // --- Change status to Closed ---
    await page.getByRole('button', { name: 'Change Status' }).first().click()
    // The period form should appear; change status dropdown to Closed
    const statusSelect = page.getByLabel('Status')
    await statusSelect.selectOption('Closed')
    await page.getByRole('button', { name: 'Save' }).click()

    await expectToast(page, 'Period status updated')

    await expect(page.getByText('Closed')).toBeVisible({ timeout: 5_000 })

    // --- Delete period ---
    await page.getByRole('button', { name: 'Delete Period' }).first().click()
    await page.getByRole('button', { name: 'Confirm' }).click()

    await expectToast(page, 'Period deleted successfully')

    await expect(page.getByText('January 2024')).not.toBeVisible({ timeout: 5_000 })
  })
})
