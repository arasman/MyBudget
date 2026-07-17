import http from '@/api/axios'
import type {
  CycleListItem,
  CycleDetail,
  CreateCyclePayload,
  UpdateCyclePayload,
} from '../types'

const base = (budgetId: string) => `/api/budgets/${budgetId}/cycles`

/** GET /api/budgets/:budgetId/cycles */
export async function list(
  budgetId: string,
  opts?: { includeDeleted?: boolean },
): Promise<CycleListItem[]> {
  const { data } = await http.get<CycleListItem[]>(base(budgetId), {
    params: opts?.includeDeleted ? { includeDeleted: true } : undefined,
  })
  return data
}

/** GET /api/budgets/:budgetId/cycles/:cycleId */
export async function get(budgetId: string, cycleId: string): Promise<CycleDetail> {
  const { data } = await http.get<CycleDetail>(`${base(budgetId)}/${cycleId}`)
  return data
}

/** POST /api/budgets/:budgetId/cycles → returns the created cycle id */
export async function create(
  budgetId: string,
  payload: CreateCyclePayload,
): Promise<{ id: string }> {
  const { data } = await http.post<{ id: string }>(base(budgetId), payload)
  return data
}

/** PUT /api/budgets/:budgetId/cycles/:cycleId */
export async function update(
  budgetId: string,
  cycleId: string,
  payload: UpdateCyclePayload,
): Promise<void> {
  await http.put(`${base(budgetId)}/${cycleId}`, payload)
}

/** DELETE /api/budgets/:budgetId/cycles/:cycleId */
export async function remove(budgetId: string, cycleId: string): Promise<void> {
  await http.delete(`${base(budgetId)}/${cycleId}`)
}

/** PUT /api/budgets/:budgetId/active-cycle  body: { cycleId } */
export async function setActive(budgetId: string, cycleId: string): Promise<void> {
  await http.put(`/api/budgets/${budgetId}/active-cycle`, { cycleId })
}

/** POST /api/budgets/:budgetId/cycles/:cycleId/restore */
export async function restore(
  budgetId: string,
  cycleId: string,
  includeExecutionRecords = false,
): Promise<void> {
  await http.post(`${base(budgetId)}/${cycleId}/restore`, null, {
    params: { includeExecutionRecords },
  })
}
