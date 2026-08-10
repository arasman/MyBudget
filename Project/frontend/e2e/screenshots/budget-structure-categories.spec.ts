import { test, expect } from '@playwright/test'
import { shoot, flushManifest } from './capture'
import { seedOwnerAndLogin, expectToast, dismissToasts } from './helpers'

/**
 * Slide screenshots — Budget Structure: Category groups + categories,
 * including CATEGORY_GROUP_NAME_DUPLICATE / CATEGORY_NAME_DUPLICATE error
 * paths and soft-delete/restore. Images land in docs/slides/budget-structure-categories/.
 */
const FLOW = 'budget-structure-categories'

test.describe('Slides — Budget Structure Categories', () => {
  test.afterAll(() => flushManifest(FLOW))

  test('group create → duplicate error → category create → duplicate error → delete → restore', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'slide-cats')

    await page.goto(`/budgets/${budgetId}/categories`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/categories`, { timeout: 10_000 })
    await shoot(page, FLOW, 1, 'list-empty', 'Categories — empty list', 'The categories tab for a freshly created budget, before any group exists.')

    // --- Create group ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Group' }).click()
    await expect(page.getByLabel('Name')).toBeVisible({ timeout: 3_000 })
    await page.getByLabel('Name').fill('Household')
    await shoot(page, FLOW, 2, 'create-group-form', 'Create group — form filled', 'The new-group modal filled with a name.')

    const [groupResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/category-groups') && r.request().method() === 'POST', { timeout: 8_000 }),
      page.getByRole('button', { name: 'Save' }).click(),
    ])
    expect(groupResp.status()).toBe(201)
    await expectToast(page, 'Category group created successfully')
    await shoot(page, FLOW, 3, 'create-group-success', 'Create group — success', 'Success toast and the new group listed.')
    await dismissToasts(page)

    // --- Duplicate group name error ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Group' }).click()
    await page.getByLabel('Name').fill('Household')
    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'A category group with this name already exists')
    await shoot(page, FLOW, 4, 'create-group-duplicate-error', 'Create group — duplicate name error', 'Reusing an existing group name in the same budget is rejected with CATEGORY_GROUP_NAME_DUPLICATE.')
    await dismissToasts(page)
    await page.getByRole('button', { name: 'Cancel' }).click()

    // --- Create category ---
    await page.getByRole('button', { name: /\+ New Category/i }).first().click()
    await expect(page.getByLabel('Name')).toBeVisible({ timeout: 3_000 })
    await page.getByLabel('Name').fill('Groceries')
    await shoot(page, FLOW, 5, 'create-category-form', 'Create category — form filled', 'The new-category modal, filled, nested under the Household group.')

    const [catResp] = await Promise.all([
      page.waitForResponse((r) => r.url().includes('/categories') && r.request().method() === 'POST', { timeout: 8_000 }),
      page.getByRole('button', { name: 'Save' }).click(),
    ])
    expect(catResp.status()).toBe(201)
    await expectToast(page, 'Category created successfully')
    await shoot(page, FLOW, 6, 'create-category-success', 'Create category — success', 'Success toast and the new category listed under its group.')
    await dismissToasts(page)

    // --- Duplicate category name error (same group) ---
    await page.getByRole('button', { name: /\+ New Category/i }).first().click()
    await page.getByLabel('Name').fill('Groceries')
    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'A category with this name already exists in this group')
    await shoot(page, FLOW, 7, 'create-category-duplicate-error', 'Create category — duplicate name error', 'Reusing an existing category name in the same group is rejected with CATEGORY_NAME_DUPLICATE.')
    await dismissToasts(page)
    await page.getByRole('button', { name: 'Cancel' }).click()

    // --- Delete category ---
    await page.getByRole('button', { name: 'Delete Category' }).first().click()
    await shoot(page, FLOW, 8, 'delete-category-confirm', 'Delete category — confirm dialog', 'The destructive-action confirmation before a soft-delete.')

    await page.getByRole('button', { name: 'Confirm' }).click()
    await expectToast(page, 'Category deleted successfully')
    await shoot(page, FLOW, 9, 'delete-category-success', 'Delete category — success', 'Soft-deleted category stays listed with a "Deleted" badge and a Restore action.')
    await dismissToasts(page)

    // --- Restore category ---
    await page.getByRole('button', { name: 'Restore' }).first().click()
    await expectToast(page, 'Category restored successfully')
    await shoot(page, FLOW, 10, 'restore-category-success', 'Restore category — success', 'The category is active again after restore.')
  })
})
