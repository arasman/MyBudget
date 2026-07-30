import { test, expect } from '@playwright/test'
import { seedBudgetCtx, createBankAccount, GTQ_CURRENCY_ID } from './helpers'

/**
 * E2E: BankAccount CRUD — create, list, update, soft-delete.
 * Specs: CS-8
 */
test.describe('BankAccount CRUD', () => {
  test('create account → appears in list', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'ba-create')

    const id = await createBankAccount(request, ctx, { alias: 'Caja GTQ' })
    expect(id).toBeTruthy()

    const listResp = await request.get(`/api/budgets/${ctx.budgetId}/bank-accounts`, {
      headers: ctx.headers,
    })
    expect(listResp.status()).toBe(200)
    const items = await listResp.json()
    const found = (items as { id: string; alias: string }[]).find((a) => a.id === id)
    expect(found).toBeDefined()
    expect(found!.alias).toBe('Caja GTQ')
  })

  test('update account alias → persisted', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'ba-update')
    const id = await createBankAccount(request, ctx, { alias: 'Original Alias' })

    const updateResp = await request.put(
      `/api/budgets/${ctx.budgetId}/bank-accounts/${id}`,
      {
        headers: ctx.headers,
        data: { alias: 'Updated Alias', isPositive: true, displayOrder: 0 },
      },
    )
    expect(updateResp.status()).toBe(200)

    const listResp = await request.get(`/api/budgets/${ctx.budgetId}/bank-accounts`, {
      headers: ctx.headers,
    })
    const items = await listResp.json()
    const found = (items as { id: string; alias: string }[]).find((a) => a.id === id)
    expect(found!.alias).toBe('Updated Alias')
  })

  test('soft-delete account → excluded from list', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'ba-delete')
    const id = await createBankAccount(request, ctx, { alias: 'To Delete' })

    const deleteResp = await request.delete(
      `/api/budgets/${ctx.budgetId}/bank-accounts/${id}`,
      { headers: ctx.headers },
    )
    expect(deleteResp.status()).toBe(204)

    const listResp = await request.get(`/api/budgets/${ctx.budgetId}/bank-accounts`, {
      headers: ctx.headers,
    })
    const items = await listResp.json()
    const found = (items as { id: string }[]).find((a) => a.id === id)
    expect(found).toBeUndefined()
  })

  test('soft-deleted account absent from new cut draft', async ({ request }) => {
    const ctx = await seedBudgetCtx(request, 'ba-draft-after-delete')
    const id = await createBankAccount(request, ctx, { alias: 'Deleted Account' })

    // Soft-delete the account
    await request.delete(`/api/budgets/${ctx.budgetId}/bank-accounts/${id}`, {
      headers: ctx.headers,
    })

    // Get draft for today
    const today = new Date().toISOString().slice(0, 10)
    const draftResp = await request.get(
      `/api/budgets/${ctx.budgetId}/cut-records/${today}`,
      { headers: ctx.headers },
    )
    expect(draftResp.status()).toBe(200)
    const draft = await draftResp.json()
    const foundInDraft = (draft.accounts as { bankAccountId: string }[]).find(
      (a) => a.bankAccountId === id,
    )
    expect(foundInDraft).toBeUndefined()
  })
})
