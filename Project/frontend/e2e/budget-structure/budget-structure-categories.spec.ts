import { test, expect } from '@playwright/test'
import { seedOwnerAndLogin } from './helpers'

/**
 * E2E: Category structure — create group → add categories → delete
 *
 * Prerequisites: Docker Compose stack running.
 */
test.describe('Budget Structure — Categories', () => {
  test('create group → add categories → delete category → delete group', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'categories')

    // Navigate to categories tab
    await page.goto(`/budgets/${budgetId}/categories`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/categories`, { timeout: 10_000 })

    // --- Create category group ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Group' }).click()
    await expect(page.getByLabel('Name')).toBeVisible({ timeout: 3_000 })
    await page.getByLabel('Name').fill('Expenses')
    const [groupResp] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/category-groups') && r.request().method() === 'POST',
        { timeout: 8_000 },
      ),
      page.getByRole('button', { name: 'Save' }).click(),
    ])
    expect(groupResp.status()).toBe(201)

    await expect(page.getByText('Expenses')).toBeVisible({ timeout: 5_000 })

    // --- Add category "Food" to group ---
    await page.getByRole('button', { name: /\+ New Category/i }).first().click()
    await expect(page.getByLabel('Name')).toBeVisible({ timeout: 3_000 })
    await page.getByLabel('Name').fill('Food')
    const [catRespFood] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/categories') && r.request().method() === 'POST',
        { timeout: 8_000 },
      ),
      page.getByRole('button', { name: 'Save' }).click(),
    ])
    expect(catRespFood.status()).toBe(201)

    await expect(page.getByText('Food')).toBeVisible({ timeout: 5_000 })

    // --- Add category "Transport" to group ---
    await page.getByRole('button', { name: /\+ New Category/i }).first().click()
    await expect(page.getByLabel('Name')).toBeVisible({ timeout: 3_000 })
    await page.getByLabel('Name').fill('Transport')
    const [catRespTransport] = await Promise.all([
      page.waitForResponse(
        (r) => r.url().includes('/categories') && r.request().method() === 'POST',
        { timeout: 8_000 },
      ),
      page.getByRole('button', { name: 'Save' }).click(),
    ])
    expect(catRespTransport.status()).toBe(201)

    await expect(page.getByText('Transport')).toBeVisible({ timeout: 5_000 })

    // --- Delete one category ---
    // Click "Delete Category" for the first category (Food)
    const deleteCatBtns = page.getByRole('button', { name: 'Delete Category' })
    await deleteCatBtns.first().click()
    await page.getByRole('button', { name: 'Confirm' }).click()

    await expect(page.getByText('Food')).not.toBeVisible({ timeout: 5_000 })

    // --- Delete the group (should remove remaining categories) ---
    await page.getByRole('button', { name: 'Delete Group' }).first().click()
    await page.getByRole('button', { name: 'Confirm' }).click()

    await expect(page.getByText('Expenses')).not.toBeVisible({ timeout: 5_000 })
  })
})
