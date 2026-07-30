import { test, expect } from '@playwright/test'
import { seedBudgetCtx, createBankAccount, upsertCutRecord } from './helpers'

/**
 * E2E: Prev/next navigation between existing cut records.
 * Spec: CS-7
 */
test.describe('CutRecord — Navigation', () => {
  test('3 cuts created — ListCutDates returns them ascending', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'cs-nav')
    const accId = await createBankAccount(request, ctx)

    const dates = ['2026-03-20', '2026-06-25', '2026-09-28']
    for (const date of dates) {
      await upsertCutRecord(request, ctx, date, [{ bankAccountId: accId, balance: 100 }])
    }

    const datesResp = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/dates`,
      { headers: ctx.headers },
    )
    expect(datesResp.status()).toBe(200)
    const list = await datesResp.json()
    expect(list).toEqual(dates)
  })

  test('navigating prev/next: GET each date in sequence returns correct data', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'cs-nav-seq')
    const accId = await createBankAccount(request, ctx)

    await upsertCutRecord(request, ctx, '2026-03-01', [{ bankAccountId: accId, balance: 100 }])
    await upsertCutRecord(request, ctx, '2026-06-01', [{ bankAccountId: accId, balance: 200 }])
    await upsertCutRecord(request, ctx, '2026-09-01', [{ bankAccountId: accId, balance: 300 }])

    // Simulate clicking "next" from first to second
    const midResp = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/2026-06-01`,
      { headers: ctx.headers },
    )
    const mid = await midResp.json()
    expect(mid.cutDate).toBe('2026-06-01')
    const midAcc = (mid.accounts as { bankAccountId: string; balance: number }[]).find(
      (a) => a.bankAccountId === accId,
    )
    expect(midAcc!.balance).toBe(200)

    // Simulate clicking "next" from second to third
    const lastResp = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/2026-09-01`,
      { headers: ctx.headers },
    )
    const last = await lastResp.json()
    expect(last.cutDate).toBe('2026-09-01')
    const lastAcc = (last.accounts as { bankAccountId: string; balance: number }[]).find(
      (a) => a.bankAccountId === accId,
    )
    expect(lastAcc!.balance).toBe(300)
  })
})
