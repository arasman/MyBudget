import { test, expect } from '@playwright/test'
import { seedBudgetContext, createExecution } from './helpers'

/**
 * E2E: Execution totals — per-line and per-category aggregation.
 * Verifies netAmount formula: Expenses + DebitNotes − CreditNotes.
 *
 * REQ-EXEC-TOTALS-1, REQ-EXEC-TOTALS-2
 */
test.describe('Budget Execution — Period Totals', () => {
  test('multiple entries → totals reflect correct amounts', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'totals')
    const headers = { Authorization: `Bearer ${ctx.token}` }

    // Expense=100, CreditNote=20, DebitNote=30 → netAmount = 100 + 30 - 20 = 110
    await createExecution(request, ctx, { entryType: 1, amount: 100 })
    await createExecution(request, ctx, { entryType: 2, amount: 20, note: 'credit' })
    await createExecution(request, ctx, { entryType: 3, amount: 30, note: 'debit' })

    const resp = await request.get(
      `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/execution-totals`,
      { headers },
    )
    expect(resp.status()).toBe(200)
    const body = await resp.json()

    expect(body.lineTotals).toBeDefined()
    expect(body.categoryTotals).toBeDefined()
    expect(body.lineTotals.length).toBeGreaterThan(0)

    const lineTotals = body.lineTotals as {
      budgetLineId: string
      totalExpenses: number
      totalCreditNotes: number
      totalDebitNotes: number
      netTotal: number
    }[]

    const lineTotal = lineTotals.find((l) => l.budgetLineId === ctx.lineId)
    expect(lineTotal).toBeDefined()
    expect(lineTotal!.totalExpenses).toBe(100)
    expect(lineTotal!.totalCreditNotes).toBe(20)
    expect(lineTotal!.totalDebitNotes).toBe(30)
    expect(lineTotal!.netTotal).toBe(110)
  })
})
