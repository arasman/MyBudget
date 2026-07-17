import { test, expect } from '@playwright/test'
import {
  seedOwnerAndLogin,
  seedDeletedCategoryGroup,
  seedDeletedCategory,
  expectToast,
} from './helpers'

/**
 * E2E: Category structure — create group → add categories → delete
 *
 * Prerequisites: Docker Compose stack running.
 */
test.describe('Budget Structure — Categories', () => {
  test.describe('soft-delete / restore', () => {
    test('toggle ON reveals deleted category group', async ({ page }) => {
      const { budgetId } = await seedOwnerAndLogin(page, 'cats-sd-group-toggle')
      const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')

      const deletedGroupId = await seedDeletedCategoryGroup(page, budgetId, token)

      await page.goto(`/budgets/${budgetId}/categories`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/categories`, { timeout: 10_000 })

      // Deleted group must NOT be visible with toggle OFF (default)
      await expect(page.getByText('Deleted Group').first()).not.toBeVisible({ timeout: 5_000 })

      // Toggle ON
      await page.getByLabel('Show deleted').check()

      await expect(page.locator(`[data-id="${deletedGroupId}"], tr, li`).filter({ hasText: 'Deleted Group' }).first()).toBeVisible({ timeout: 5_000 })
    })

    test('toggle OFF hides deleted category group', async ({ page }) => {
      const { budgetId } = await seedOwnerAndLogin(page, 'cats-sd-group-off')
      const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')

      await seedDeletedCategoryGroup(page, budgetId, token)

      await page.goto(`/budgets/${budgetId}/categories`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/categories`, { timeout: 10_000 })

      // Toggle ON first
      await page.getByLabel('Show deleted').check()
      await expect(page.getByText('Deleted Group').first()).toBeVisible({ timeout: 5_000 })

      // Toggle OFF — group must disappear
      await page.getByLabel('Show deleted').uncheck()
      await expect(page.getByText('Deleted Group').first()).not.toBeVisible({ timeout: 5_000 })
    })

    test('toggle ON reveals deleted category', async ({ page }) => {
      const { budgetId } = await seedOwnerAndLogin(page, 'cats-sd-cat-toggle')
      const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')

      // Need a live group to hold the deleted category
      const groupResp = await page.request.post(`/api/budgets/${budgetId}/category-groups`, {
        headers: { Authorization: `Bearer ${token}` },
        data: { name: 'Active Group', displayOrder: 1 },
      })
      expect(groupResp.status()).toBe(201)
      const { id: groupId } = await groupResp.json()

      await seedDeletedCategory(page, budgetId, groupId, token)

      await page.goto(`/budgets/${budgetId}/categories`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/categories`, { timeout: 10_000 })

      // Deleted category must NOT be visible with toggle OFF (default)
      await expect(page.getByText('Deleted Category').first()).not.toBeVisible({ timeout: 5_000 })

      // Toggle ON
      await page.getByLabel('Show deleted').check()
      await expect(page.getByText('Deleted Category').first()).toBeVisible({ timeout: 5_000 })
    })

    test('restore category group reappears in active list with success toast', async ({ page }) => {
      const { budgetId } = await seedOwnerAndLogin(page, 'cats-restore-group')
      const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')

      await seedDeletedCategoryGroup(page, budgetId, token)

      await page.goto(`/budgets/${budgetId}/categories`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/categories`, { timeout: 10_000 })

      // Toggle ON to reveal deleted group
      await page.getByLabel('Show deleted').check()
      await expect(page.getByText('Deleted Group').first()).toBeVisible({ timeout: 5_000 })

      // Click Restore on the deleted group
      await page.getByRole('button', { name: 'Restore' }).first().click()

      await expectToast(page, 'Category group restored successfully')

      // Toggle OFF — restored group must appear in active list
      await page.getByLabel('Show deleted').uncheck()
      await expect(page.getByText('Deleted Group').first()).toBeVisible({ timeout: 5_000 })
    })

    test('restore category reappears under its group with success toast', async ({ page }) => {
      const { budgetId } = await seedOwnerAndLogin(page, 'cats-restore-cat')
      const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')

      // Create a live group to hold the deleted category
      const groupResp = await page.request.post(`/api/budgets/${budgetId}/category-groups`, {
        headers: { Authorization: `Bearer ${token}` },
        data: { name: 'Parent Group', displayOrder: 1 },
      })
      expect(groupResp.status()).toBe(201)
      const { id: groupId } = await groupResp.json()

      await seedDeletedCategory(page, budgetId, groupId, token)

      await page.goto(`/budgets/${budgetId}/categories`)
      await expect(page).toHaveURL(`/budgets/${budgetId}/categories`, { timeout: 10_000 })

      // Toggle ON to reveal deleted category
      await page.getByLabel('Show deleted').check()
      await expect(page.getByText('Deleted Category').first()).toBeVisible({ timeout: 5_000 })

      // Click Restore on the deleted category
      await page.getByRole('button', { name: 'Restore' }).first().click()

      await expectToast(page, 'Category restored successfully')

      // Toggle OFF — restored category must appear under its group
      await page.getByLabel('Show deleted').uncheck()
      await expect(page.getByText('Deleted Category').first()).toBeVisible({ timeout: 5_000 })
    })
  })

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

    await expectToast(page, 'Category group created successfully')

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

    await expectToast(page, 'Category created successfully')

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

    await expectToast(page, 'Category created successfully')

    await expect(page.getByText('Transport')).toBeVisible({ timeout: 5_000 })

    // --- Delete one category ---
    // Click "Delete Category" for the first category (Food)
    const deleteCatBtns = page.getByRole('button', { name: 'Delete Category' })
    await deleteCatBtns.first().click()
    await page.getByRole('button', { name: 'Confirm' }).click()

    await expectToast(page, 'Category deleted successfully')

    await expect(page.getByText('Food')).not.toBeVisible({ timeout: 5_000 })

    // --- Delete the group (should remove remaining categories) ---
    await page.getByRole('button', { name: 'Delete Group' }).first().click()
    await page.getByRole('button', { name: 'Confirm' }).click()

    await expectToast(page, 'Category group deleted successfully')

    await expect(page.getByText('Expenses')).not.toBeVisible({ timeout: 5_000 })
  })
})
