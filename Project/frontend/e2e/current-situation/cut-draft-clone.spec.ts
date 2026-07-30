import { test, expect } from '@playwright/test'
import { seedBudgetCtx, createBankAccount, upsertCutRecord, GTQ_CURRENCY_ID } from './helpers'

/**
 * E2E: Clone-from-previous cut behavior (CS-2).
 *
 * Verifies:
 * - Balance from cut A is cloned to cut B's draft for matching accounts.
 * - Newly-added account B (after cut A) gets balance = 0 in cut B draft.
 * - Soft-deleted account is absent from cut B draft.
 */
test.describe('CutRecord — Draft Clone', () => {
  test('draft for date B clones balances from date A for matching accounts', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'cs-clone')

    // Account A exists before cut A
    const accAId = await createBankAccount(request, ctx, { alias: 'Account A', displayOrder: 0 })

    const dateA = '2026-05-01'
    await upsertCutRecord(request, ctx, dateA, [{ bankAccountId: accAId, balance: 1234.56 }])

    // Account B created AFTER cut A
    const accBId = await createBankAccount(request, ctx, { alias: 'Account B', displayOrder: 1 })

    // Get draft for date B (later) — should clone A's balance
    const dateB = '2026-06-01'
    const draftResp = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/${dateB}`,
      { headers: ctx.headers },
    )
    expect(draftResp.status()).toBe(200)
    const draft = await draftResp.json()
    expect(draft.isDraft).toBe(true)

    const accounts = draft.accounts as { bankAccountId: string; balance: number }[]

    // Account A: balance cloned from cut A
    const a = accounts.find((x) => x.bankAccountId === accAId)
    expect(a).toBeDefined()
    expect(a!.balance).toBe(1234.56)

    // Account B: newly added — balance 0
    const b = accounts.find((x) => x.bankAccountId === accBId)
    expect(b).toBeDefined()
    expect(b!.balance).toBe(0)
  })

  test('soft-deleted account excluded from draft clone', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'cs-clone-del')

    const accCId = await createBankAccount(request, ctx, { alias: 'Account C' })

    const dateA = '2026-04-01'
    await upsertCutRecord(request, ctx, dateA, [{ bankAccountId: accCId, balance: 500 }])

    // Soft-delete account C
    await request.delete(`/api/budgets/${ctx.budgetId}/bank-accounts/${accCId}`, {
      headers: ctx.headers,
    })

    const dateB = '2026-05-01'
    const draftResp = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/${dateB}`,
      { headers: ctx.headers },
    )
    const draft = await draftResp.json()
    const accounts = draft.accounts as { bankAccountId: string }[]

    // Soft-deleted account C must not appear
    const c = accounts.find((x) => x.bankAccountId === accCId)
    expect(c).toBeUndefined()
  })
})
