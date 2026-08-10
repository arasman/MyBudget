import { test, expect } from '@playwright/test'
import { shoot, flushManifest } from './capture'
import { seedOwnerAndLogin, expectToast, dismissToasts, createBankAccountViaApi } from './helpers'

/**
 * Slide screenshots — Current Situation (cut records): draft pre-population,
 * the "no active period" save error, save success, and delete (typed-confirmation).
 * Images land in docs/slides/current-situation/.
 */
const FLOW = 'current-situation'

test.describe('Slides — Current Situation', () => {
  test.afterAll(() => flushManifest(FLOW))

  test('draft → save error (no period) → save success → delete', async ({ page }) => {
    const { budgetId } = await seedOwnerAndLogin(page, 'slide-cut')
    await createBankAccountViaApi(page, budgetId, 'Caja GTQ')

    await page.goto(`/budgets/${budgetId}/current-situation`)
    await expect(page).toHaveURL(`/budgets/${budgetId}/current-situation`, { timeout: 10_000 })
    await expect(page.getByText('Caja GTQ')).toBeVisible({ timeout: 10_000 })
    await shoot(page, FLOW, 1, 'draft-form', 'Current Situation — draft form', 'A new cut, pre-populated with active bank accounts at balance 0 (Draft badge shown).')

    // --- Fill balances (no active period yet) ---
    const row = page.getByText('Caja GTQ', { exact: true }).locator('..')
    await row.locator('input[inputmode="decimal"]').fill('1500.50')
    await page.locator('input[type="number"]').fill('7.8')
    await shoot(page, FLOW, 2, 'form-filled', 'Current Situation — form filled', 'Balance and exchange rate entered, ready to save.')

    // --- Save error: no active period covers this date yet ---
    await page.getByRole('button', { name: 'Save' }).click()
    await expect(page.getByText('No active budget period covers this cut date.')).toBeVisible({ timeout: 5_000 })
    await shoot(page, FLOW, 3, 'save-error', 'Save — no active period error', 'Saving fails with a 422 when no budget period covers the cut date yet.')

    // --- Seed a period covering today, then retry save ---
    const token = await page.evaluate(() => localStorage.getItem('accessToken') ?? '')
    const headers = { Authorization: `Bearer ${token}` }
    const year = new Date().getFullYear()
    const cycleResp = await page.request.post(`/api/budgets/${budgetId}/cycles`, {
      headers,
      data: { name: `${year} Cycle`, startDate: `${year}-01-01`, endDate: `${year}-12-31`, defaultCurrencyId: '11111111-1111-1111-1111-111111111111' },
    })
    expect(cycleResp.status()).toBe(201)
    const { id: cycleId } = await cycleResp.json()
    const periodResp = await page.request.post(`/api/budgets/${budgetId}/cycles/${cycleId}/periods`, {
      headers,
      data: { name: `${year} Period`, periodNumber: 1, startDate: `${year}-01-01`, endDate: `${year}-12-31` },
    })
    expect(periodResp.status()).toBe(201)

    await page.getByRole('button', { name: 'Save' }).click()
    await expectToast(page, 'Cut record saved')
    await shoot(page, FLOW, 4, 'save-success', 'Save — success', 'Success toast; the totals panel reflects the saved balances and exchange rate.')
    await dismissToasts(page)

    // --- Delete (typed-confirmation) ---
    await page.getByRole('button', { name: 'Delete Cut' }).click()
    await shoot(page, FLOW, 5, 'delete-confirm-empty', 'Delete cut — confirm dialog', 'Deletion requires typing the exact cut date to confirm — the button starts disabled.')

    const cutDate = new Date().toISOString().slice(0, 10)
    await page.getByRole('dialog').locator('input[type="text"]').fill(cutDate)
    await shoot(page, FLOW, 6, 'delete-confirm-typed', 'Delete cut — date typed', 'Once the typed date matches, the delete button becomes enabled.')

    await page.getByRole('button', { name: 'Delete permanently' }).click()
    await expectToast(page, 'Cut record deleted')
    await shoot(page, FLOW, 7, 'delete-success', 'Delete cut — success', 'Cut record deleted; success toast shown.')
  })
})
