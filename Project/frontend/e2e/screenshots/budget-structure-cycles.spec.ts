import { test, expect } from '@playwright/test'
import { shoot, flushManifest } from './capture'
import { seedOwnerAndLogin, expectToast, dismissToasts } from './helpers'

/**
 * Slide screenshots — Budget Structure: Cycles CRUD, including the
 * CYCLE_NAME_DUPLICATE error path. Images land in docs/slides/budget-structure-cycles/.
 */
const FLOW = 'budget-structure-cycles'

test.describe('Slides — Budget Structure Cycles', () => {
  test.afterAll(() => flushManifest(FLOW))

  test('create → duplicate name error → edit → set active → delete', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'slide-cycles')

    await page.goto(`/budgets/${budgetId}/cycles`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/cycles`, { timeout: 10_000 })
    await shoot(page, FLOW, 1, 'list-empty', 'Cycles — empty list', 'The cycles list for a freshly created budget, before any cycle exists.')

    // --- Create ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Cycle' }).click()
    await page.getByLabel('Name').fill('2026 Budget')
    await page.getByLabel('Start Date').fill('2026-01-01')
    await page.getByLabel('End Date').fill('2026-12-31')
    await shoot(page, FLOW, 2, 'create-form', 'Create cycle — form filled', 'The new-cycle modal filled with a name and date range.')

    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'Cycle created successfully')
    await shoot(page, FLOW, 3, 'create-success', 'Create cycle — success', 'Success toast and the new cycle listed.')
    await dismissToasts(page)

    // --- Duplicate name error ---
    await page.getByRole('navigation').getByRole('button', { name: 'New Cycle' }).click()
    await page.getByLabel('Name').fill('2026 Budget')
    await page.getByLabel('Start Date').fill('2027-01-01')
    await page.getByLabel('End Date').fill('2027-12-31')
    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'A cycle with this name already exists in this budget')
    await shoot(page, FLOW, 4, 'create-duplicate-error', 'Create cycle — duplicate name error', 'Reusing an existing cycle name in the same budget is rejected with CYCLE_NAME_DUPLICATE.')
    await dismissToasts(page)

    // --- Edit ---
    // Error toast leaves the create modal open (by design — user can fix and retry).
    await page.getByRole('button', { name: 'Cancel' }).click()
    await page.getByRole('button', { name: 'Edit Cycle' }).first().click()
    await page.getByLabel('Name').clear()
    await page.getByLabel('Name').fill('2026 Budget — Updated')
    await shoot(page, FLOW, 5, 'edit-form', 'Edit cycle — form', 'Inline edit form with the name changed.')

    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'Cycle updated successfully')
    await shoot(page, FLOW, 6, 'edit-success', 'Edit cycle — success', 'Success toast and the updated name reflected in the list.')
    await dismissToasts(page)

    // --- Set active ---
    const setActiveBtn = page.getByRole('button', { name: 'Set as Active' }).first()
    if (await setActiveBtn.isEnabled()) {
      await setActiveBtn.click()
      await expectToast(page, 'Cycle set as active')
      await shoot(page, FLOW, 7, 'set-active-success', 'Set active cycle — success', 'The cycle marked Active, driving which cycle budget-execution defaults to.')
      await dismissToasts(page)
    }

    // --- Delete ---
    await page.getByRole('button', { name: 'Delete Cycle' }).first().click()
    await shoot(page, FLOW, 8, 'delete-confirm', 'Delete cycle — confirm dialog', 'The destructive-action confirmation before a soft-delete.')

    await page.getByRole('button', { name: 'Confirm' }).click()
    await expectToast(page, 'Cycle deleted successfully')
    await shoot(page, FLOW, 9, 'delete-success', 'Delete cycle — success', 'Cycle soft-deleted; success toast shown.')
  })
})
