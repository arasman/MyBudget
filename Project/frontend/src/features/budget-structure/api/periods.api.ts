import http from '@/api/axios'
import type {
  CreatePeriodPayload,
  UpdatePeriodPayload,
  PatchPeriodStatusPayload,
  PeriodSummary,
} from '../types'

const base = (budgetId: string, cycleId: string) =>
  `/api/budgets/${budgetId}/cycles/${cycleId}/periods`

/** GET /api/budgets/:budgetId/cycles/:cycleId/periods */
export async function list(
  budgetId: string,
  cycleId: string,
  opts?: { includeDeleted?: boolean },
): Promise<PeriodSummary[]> {
  const { data } = await http.get<PeriodSummary[]>(base(budgetId, cycleId), {
    params: opts?.includeDeleted ? { includeDeleted: true } : undefined,
  })
  return data
}

/** POST /api/budgets/:budgetId/cycles/:cycleId/periods → returns the created period id */
export async function create(
  budgetId: string,
  cycleId: string,
  payload: CreatePeriodPayload,
): Promise<{ id: string }> {
  const { data } = await http.post<{ id: string }>(base(budgetId, cycleId), payload)
  return data
}

/** PUT /api/budgets/:budgetId/cycles/:cycleId/periods/:periodId */
export async function update(
  budgetId: string,
  cycleId: string,
  periodId: string,
  payload: UpdatePeriodPayload,
): Promise<void> {
  await http.put(`${base(budgetId, cycleId)}/${periodId}`, payload)
}

/** PATCH /api/budgets/:budgetId/cycles/:cycleId/periods/:periodId/status */
export async function patchStatus(
  budgetId: string,
  cycleId: string,
  periodId: string,
  payload: PatchPeriodStatusPayload,
): Promise<void> {
  await http.patch(`${base(budgetId, cycleId)}/${periodId}/status`, payload)
}

/** DELETE /api/budgets/:budgetId/cycles/:cycleId/periods/:periodId */
export async function remove(
  budgetId: string,
  cycleId: string,
  periodId: string,
): Promise<void> {
  await http.delete(`${base(budgetId, cycleId)}/${periodId}`)
}

/** POST /api/budgets/:budgetId/cycles/:cycleId/periods/:periodId/restore */
export async function restore(
  budgetId: string,
  cycleId: string,
  periodId: string,
  includeExecutionRecords = false,
): Promise<void> {
  await http.post(`${base(budgetId, cycleId)}/${periodId}/restore`, null, {
    params: { includeExecutionRecords },
  })
}
