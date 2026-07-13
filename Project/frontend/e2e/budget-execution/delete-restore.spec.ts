import { test, expect } from '@playwright/test'
import { seedBudgetContext, createExecution, deleteExecution } from './helpers'

/**
 * E2E: Delete and Restore ExecutionRecord.
 * Verifies the full soft-delete and restore cycle.
 *
 * REQ-EXEC-DELETE-1, REQ-EXEC-RESTORE-1
 */
test.describe('Budget Execution — Delete and Restore', () => {
  test('delete execution → gone from list → restore → back in list', async ({ request }) => {
    const ctx = await seedBudgetContext(request, 'del-restore')
    const headers = { Authorization: `Bearer ${ctx.token}` }
    const listUrl = `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions`
    const restoreBase = `/api/budgets/${ctx.budgetId}/periods/${ctx.periodId}/budget-lines/${ctx.lineId}/executions`

    // Create an execution record
    const execId = await createExecution(request, ctx, { amount: 150 })

    // Verify it exists
    const beforeList = await request.get(listUrl, { headers })
    const beforeItems = await beforeList.json()
    expect((beforeItems as { id: string }[]).some((i) => i.id === execId)).toBeTruthy()

    // Soft-delete it
    await deleteExecution(request, ctx, execId)

    // Verify it's gone from the list
    const afterDeleteList = await request.get(listUrl, { headers })
    const afterDeleteItems = await afterDeleteList.json()
    expect((afterDeleteItems as { id: string }[]).some((i) => i.id === execId)).toBeFalsy()

    // Restore it
    const restoreResp = await request.post(`${restoreBase}/${execId}/restore`, {
      headers,
    })
    expect(restoreResp.status()).toBe(200)

    // Verify it's back in the list
    const afterRestoreList = await request.get(listUrl, { headers })
    const afterRestoreItems = await afterRestoreList.json()
    expect((afterRestoreItems as { id: string }[]).some((i) => i.id === execId)).toBeTruthy()
  })
})
