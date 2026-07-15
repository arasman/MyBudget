import http from '@/api/axios'
import type { BudgetLineResponse, CreateBudgetLinePayload, UpdateBudgetLinePayload } from '../types'

const base = (budgetId: string, periodId: string) =>
  `/api/budgets/${budgetId}/periods/${periodId}/lines`

/** GET /api/budgets/:budgetId/periods/:periodId/lines → returns all lines for the period */
export async function list(
  budgetId: string,
  periodId: string,
  includeDeleted = false,
): Promise<BudgetLineResponse[]> {
  const { data } = await http.get<BudgetLineResponse[]>(base(budgetId, periodId), {
    params: includeDeleted ? { includeDeleted: true } : undefined,
  })
  return data
}

/** POST /api/budgets/:budgetId/periods/:periodId/lines → returns created line id */
export async function create(
  budgetId: string,
  periodId: string,
  payload: CreateBudgetLinePayload,
): Promise<{ id: string }> {
  const { data } = await http.post<{ id: string }>(base(budgetId, periodId), payload)
  return data
}

/** PUT /api/budgets/:budgetId/periods/:periodId/lines/:lineId */
export async function update(
  budgetId: string,
  periodId: string,
  lineId: string,
  payload: UpdateBudgetLinePayload,
): Promise<void> {
  await http.put(`${base(budgetId, periodId)}/${lineId}`, payload)
}

/** DELETE /api/budgets/:budgetId/periods/:periodId/lines/:lineId */
export async function remove(budgetId: string, periodId: string, lineId: string): Promise<void> {
  await http.delete(`${base(budgetId, periodId)}/${lineId}`)
}

/** PUT /api/budgets/:budgetId/periods/:periodId/budget-lines/order */
export async function reorder(
  budgetId: string,
  periodId: string,
  orderedIds: string[],
): Promise<void> {
  await http.put(`/api/budgets/${budgetId}/periods/${periodId}/budget-lines/order`, { orderedIds })
}

/** POST /api/budgets/:budgetId/periods/:periodId/lines/:lineId/restore */
export async function restore(
  budgetId: string,
  periodId: string,
  lineId: string,
  includeExecutionRecords: boolean,
): Promise<void> {
  await http.post(`${base(budgetId, periodId)}/${lineId}/restore`, null, {
    params: { includeExecutionRecords },
  })
}
