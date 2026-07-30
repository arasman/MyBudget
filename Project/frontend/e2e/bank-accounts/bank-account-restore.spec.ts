import { test, expect } from '@playwright/test'
import { seedBudgetCtx, createBankAccount } from './helpers'

/**
 * E2E: BankAccount restore flow and alias uniqueness with soft-deleted accounts.
 * Specs: BA-5, BA-1 (amended), BA-2 (amended)
 */
test.describe('BankAccount restore', () => {
  test('create → delete → restore → account is active again', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'ba-restore')
    const id = await createBankAccount(request, ctx, { alias: 'Restore Me' })

    // Soft-delete
    const deleteResp = await request.delete(
      `/api/budgets/${ctx.budgetId}/bank-accounts/${id}`,
      { headers: ctx.headers },
    )
    expect(deleteResp.status()).toBe(204)

    // Verify excluded from default list
    const listBeforeRestore = await request.get(
      `/api/budgets/${ctx.budgetId}/bank-accounts`,
      { headers: ctx.headers },
    )
    const itemsBefore = await listBeforeRestore.json()
    expect((itemsBefore as { id: string }[]).find((a) => a.id === id)).toBeUndefined()

    // Visible with includeDeleted=true
    const listDeleted = await request.get(
      `/api/budgets/${ctx.budgetId}/bank-accounts?includeDeleted=true`,
      { headers: ctx.headers },
    )
    expect(listDeleted.status()).toBe(200)
    const deletedItems = await listDeleted.json()
    const deletedAccount = (deletedItems as { id: string; deletedAt: string | null }[]).find(
      (a) => a.id === id,
    )
    expect(deletedAccount).toBeDefined()
    expect(deletedAccount!.deletedAt).not.toBeNull()

    // Restore
    const restoreResp = await request.post(
      `/api/budgets/${ctx.budgetId}/bank-accounts/${id}/restore`,
      { headers: ctx.headers },
    )
    expect(restoreResp.status()).toBe(204)

    // Account appears in default list again
    const listAfterRestore = await request.get(
      `/api/budgets/${ctx.budgetId}/bank-accounts`,
      { headers: ctx.headers },
    )
    const itemsAfter = await listAfterRestore.json()
    const restored = (itemsAfter as { id: string; deletedAt: string | null }[]).find(
      (a) => a.id === id,
    )
    expect(restored).toBeDefined()
    expect(restored!.deletedAt).toBeNull()
  })

  test('alias of soft-deleted account is rejected on create', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'ba-alias-soft')
    const id = await createBankAccount(request, ctx, { alias: 'Reserved Alias' })

    // Soft-delete it
    await request.delete(`/api/budgets/${ctx.budgetId}/bank-accounts/${id}`, {
      headers: ctx.headers,
    })

    // Attempt create with same alias
    const createResp = await request.post(`/api/budgets/${ctx.budgetId}/bank-accounts`, {
      headers: ctx.headers,
      data: {
        alias: 'Reserved Alias',
        currencyId: '11111111-1111-1111-1111-111111111111',
        isPositive: true,
        displayOrder: 2,
      },
    })
    expect(createResp.status()).toBe(422)
  })

  test('restore non-existent account returns 404', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'ba-restore-404')
    const fakeId = '00000000-0000-0000-0000-000000000001'

    const resp = await request.post(
      `/api/budgets/${ctx.budgetId}/bank-accounts/${fakeId}/restore`,
      { headers: ctx.headers },
    )
    expect(resp.status()).toBe(404)
  })

  test('restore already-active account returns 404', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'ba-restore-active')
    const id = await createBankAccount(request, ctx, { alias: 'Active Account' })

    const resp = await request.post(
      `/api/budgets/${ctx.budgetId}/bank-accounts/${id}/restore`,
      { headers: ctx.headers },
    )
    expect(resp.status()).toBe(404)
  })
})
