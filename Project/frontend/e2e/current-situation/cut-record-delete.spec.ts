import { test, expect } from '@playwright/test'
import { seedBudgetCtx, createBankAccount, upsertCutRecord } from './helpers'

/**
 * E2E: Delete cut record via API — verifies 204, 404 for non-existent,
 * and CutBankAccount rows removed (CS-4).
 *
 * The frontend delete modal (date-confirmation UI) is covered by unit tests
 * in DeleteCutModal.spec.ts. This E2E validates the underlying API contract.
 */
test.describe('CutRecord — Delete', () => {
  test('delete existing cut → 204 and record removed from dates list', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'cs-del')
    const accId = await createBankAccount(request, ctx)
    const date = '2026-07-15'
    await upsertCutRecord(request, ctx, date, [{ bankAccountId: accId, balance: 500 }])

    // Verify it exists
    const datesResp1 = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/dates`,
      { headers: ctx.headers },
    )
    const dates1 = await datesResp1.json()
    expect(dates1).toContain(date)

    // Delete
    const deleteResp = await request.delete(
      `/api/budgets/${ctx.budgetId}/cut-records/${date}`,
      { headers: ctx.headers },
    )
    expect(deleteResp.status()).toBe(204)

    // Verify removed from dates list
    const datesResp2 = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/dates`,
      { headers: ctx.headers },
    )
    const dates2 = await datesResp2.json()
    expect(dates2).not.toContain(date)
  })

  test('delete non-existent cut → 404', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'cs-del-404')

    const resp = await request.delete(
      `/api/budgets/${ctx.budgetId}/cut-records/2026-01-01`,
      { headers: ctx.headers },
    )
    expect(resp.status()).toBe(404)
  })

  test('after delete, draft for same date starts fresh with balance 0', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'cs-del-draft')
    const accId = await createBankAccount(request, ctx)
    const date = '2026-08-01'

    // Create, then delete
    await upsertCutRecord(request, ctx, date, [{ bankAccountId: accId, balance: 9999 }])
    await request.delete(`/api/budgets/${ctx.budgetId}/cut-records/${date}`, {
      headers: ctx.headers,
    })

    // Get draft — should return balance 0 again
    const draftResp = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/${date}`,
      { headers: ctx.headers },
    )
    const draft = await draftResp.json()
    expect(draft.isDraft).toBe(true)
    const acc = (draft.accounts as { bankAccountId: string; balance: number }[]).find(
      (a) => a.bankAccountId === accId,
    )
    expect(acc!.balance).toBe(0)
  })
})
