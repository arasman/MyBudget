import http from '@/api/axios'
import type { CreateExecutionRequest, ExecutionRecordDto, UpdateExecutionRequest } from '../types'

const base = (budgetId: string, periodId: string, lineId: string) =>
  `/api/budgets/${budgetId}/periods/${periodId}/budget-lines/${lineId}/executions`

/** GET /api/budgets/:budgetId/periods/:periodId/budget-lines/:lineId/executions */
export async function list(
  budgetId: string,
  periodId: string,
  lineId: string,
  includeDeleted = false,
): Promise<ExecutionRecordDto[]> {
  const { data } = await http.get<ExecutionRecordDto[]>(base(budgetId, periodId, lineId), {
    params: includeDeleted ? { includeDeleted: true } : undefined,
  })
  return data
}

/** POST /api/budgets/:budgetId/periods/:periodId/budget-lines/:lineId/executions → 201 { id } */
export async function create(
  budgetId: string,
  periodId: string,
  lineId: string,
  payload: CreateExecutionRequest,
): Promise<{ id: string }> {
  const { data } = await http.post<{ id: string }>(base(budgetId, periodId, lineId), payload)
  return data
}

/** PUT /api/budgets/:budgetId/periods/:periodId/budget-lines/:lineId/executions/:executionId */
export async function update(
  budgetId: string,
  periodId: string,
  lineId: string,
  executionId: string,
  payload: UpdateExecutionRequest,
): Promise<{ id: string }> {
  const { data } = await http.put<{ id: string }>(
    `${base(budgetId, periodId, lineId)}/${executionId}`,
    payload,
  )
  return data
}

/** DELETE /api/budgets/:budgetId/periods/:periodId/budget-lines/:lineId/executions/:executionId → 204 */
export async function remove(
  budgetId: string,
  periodId: string,
  lineId: string,
  executionId: string,
): Promise<void> {
  await http.delete(`${base(budgetId, periodId, lineId)}/${executionId}`)
}

/** POST /api/budgets/:budgetId/periods/:periodId/budget-lines/:lineId/executions/:executionId/restore */
export async function restore(
  budgetId: string,
  periodId: string,
  lineId: string,
  executionId: string,
): Promise<{ id: string }> {
  const { data } = await http.post<{ id: string }>(
    `${base(budgetId, periodId, lineId)}/${executionId}/restore`,
  )
  return data
}
