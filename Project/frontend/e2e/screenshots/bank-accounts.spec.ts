import { test, expect } from '@playwright/test'
import { shoot, flushManifest } from './capture'
import { seedOwnerAndLogin, expectToast, dismissToasts } from './helpers'

/**
 * Slide screenshots — Bank Accounts CRUD, including the ALIAS_DUPLICATE
 * error path and soft-delete/restore. Images land in docs/slides/bank-accounts/.
 */
const FLOW = 'bank-accounts'

test.describe('Slides — Bank Accounts', () => {
  test.afterAll(() => flushManifest(FLOW))

  test('create → duplicate alias error → edit → delete → restore', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'slide-bank')

    await page.goto(`/budgets/${budgetId}/bank-accounts`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/bank-accounts`, { timeout: 10_000 })
    await shoot(page, FLOW, 1, 'list-empty', 'Bank Accounts — empty list', 'The bank accounts tab for a freshly created budget, before any account exists.')

    // --- Create ---
    await page.getByRole('button', { name: '+ New Account' }).click()
    await page.getByLabel('Alias').fill('Banco Industrial GTQ')
    await page.getByLabel('Currency').selectOption({ label: 'GTQ' })
    await shoot(page, FLOW, 2, 'create-form', 'Create account — form filled', 'The new-account modal with alias and currency filled; positive-balance toggle defaults on.')

    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'Bank account created')
    await shoot(page, FLOW, 3, 'create-success', 'Create account — success', 'Success toast and the new account listed.')
    await dismissToasts(page)

    // --- Duplicate alias error ---
    await page.getByRole('button', { name: '+ New Account' }).click()
    await page.getByLabel('Alias').fill('Banco Industrial GTQ')
    await page.getByLabel('Currency').selectOption({ label: 'GTQ' })
    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'An account with this alias already exists')
    await shoot(page, FLOW, 4, 'create-duplicate-error', 'Create account — duplicate alias error', 'Reusing an existing alias in the same budget is rejected with ALIAS_DUPLICATE.')
    await dismissToasts(page)
    await page.getByRole('button', { name: 'Cancel' }).click()

    // --- Edit ---
    await page.getByRole('button', { name: 'Edit' }).first().click()
    await page.getByLabel('Alias').clear()
    await page.getByLabel('Alias').fill('Banco Industrial — Cuenta Principal')
    await shoot(page, FLOW, 5, 'edit-form', 'Edit account — form', 'Edit modal with the alias changed (currency is locked once created).')

    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'Bank account updated')
    await shoot(page, FLOW, 6, 'edit-success', 'Edit account — success', 'Success toast and the updated alias reflected in the list.')
    await dismissToasts(page)

    // --- Delete ---
    await page.getByRole('button', { name: 'Delete' }).first().click()
    await shoot(page, FLOW, 7, 'delete-confirm', 'Delete account — confirm dialog', 'The destructive-action confirmation, naming the account by alias.')

    await page.getByRole('dialog').getByRole('button', { name: 'Delete' }).click()
    await expectToast(page, 'Bank account deleted')
    await shoot(page, FLOW, 8, 'delete-success', 'Delete account — success', 'Success toast; the deleted account drops out of the default (active-only) list.')
    await dismissToasts(page)

    // --- Show deleted + restore ---
    await page.getByLabel('Show deleted').check()
    await shoot(page, FLOW, 9, 'show-deleted-toggle', 'Show deleted — toggle on', 'Toggling "Show deleted" reveals the soft-deleted account (dimmed, "deleted" badge) with a Restore action.')

    await page.getByRole('button', { name: 'Restore' }).click()
    await expectToast(page, 'Bank account restored')
    await shoot(page, FLOW, 10, 'restore-success', 'Restore account — success', 'The account is active again after restore.')
  })
})
