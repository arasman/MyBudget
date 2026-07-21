import http from '@/api/axios'
import type { BudgetLineResponse, CreateBudgetLinePayload, UpdateBudgetLinePayload } from '../types'

const base = (budgetId: string) => `/api/budgets/${budgetId}/lines`

/** GET /api/budgets/:budgetId/lines — returns all lines for the budget (no periodId) */
export async function list(
  budgetId: string,
  includeDeleted = false,
): Promise<BudgetLineResponse[]> {
  const { data } = await http.get<BudgetLineResponse[]>(base(budgetId), {
    params: includeDeleted ? { includeDeleted: true } : undefined,
  })
  return data
}

/** POST /api/budgets/:budgetId/lines — creates a new budget line */
export async function create(
  budgetId: string,
  payload: CreateBudgetLinePayload,
): Promise<{ id: string }> {
  const { data } = await http.post<{ id: string }>(base(budgetId), payload)
  return data
}

/** PUT /api/budgets/:budgetId/lines/:lineId */
export async function update(
  budgetId: string,
  lineId: string,
  payload: UpdateBudgetLinePayload,
): Promise<void> {
  await http.put(`${base(budgetId)}/${lineId}`, payload)
}

/** DELETE /api/budgets/:budgetId/lines/:lineId */
export async function remove(budgetId: string, lineId: string): Promise<void> {
  await http.delete(`${base(budgetId)}/${lineId}`)
}

/** PUT /api/budgets/:budgetId/lines/order */
export async function reorder(budgetId: string, orderedIds: string[]): Promise<void> {
  await http.put(`${base(budgetId)}/order`, { orderedIds })
}

/** POST /api/budgets/:budgetId/lines/:lineId/restore */
export async function restore(
  budgetId: string,
  lineId: string,
  includeExecutionRecords: boolean,
): Promise<void> {
  await http.post(`${base(budgetId)}/${lineId}/restore`, null, {
    params: { includeExecutionRecords },
  })
}
