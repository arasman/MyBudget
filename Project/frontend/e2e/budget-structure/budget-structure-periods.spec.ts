import { test, expect } from '@playwright/test'
import { seedOwnerAndLogin } from './helpers'

/**
 * E2E: Period management within a cycle
 *
 * Prerequisites: Docker Compose stack running.
 */
test.describe('Budget Structure — Period Management', () => {
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

    await expect(page.getByText('January 2024')).toBeVisible({ timeout: 5_000 })

    // --- Change status to Closed ---
    await page.getByRole('button', { name: 'Change Status' }).first().click()
    // The period form should appear; change status dropdown to Closed
    const statusSelect = page.getByLabel('Status')
    await statusSelect.selectOption('Closed')
    await page.getByRole('button', { name: 'Save' }).click()

    await expect(page.getByText('Closed')).toBeVisible({ timeout: 5_000 })

    // --- Delete period ---
    await page.getByRole('button', { name: 'Delete Period' }).first().click()
    await page.getByRole('button', { name: 'Confirm' }).click()

    await expect(page.getByText('January 2024')).not.toBeVisible({ timeout: 5_000 })
  })
})
