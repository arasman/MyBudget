import http from '@/api/axios'
import type { CutRecordResponse, UpsertCutRecordDto } from '../types/cutRecord'

const base = (budgetId: string) => `/api/budgets/${budgetId}/cut-records`

/** GET /api/budgets/:budgetId/cut-records/:date */
export async function getCutRecord(budgetId: string, date: string): Promise<CutRecordResponse> {
  const { data } = await http.get<CutRecordResponse>(`${base(budgetId)}/${date}`)
  return data
}

/** PUT /api/budgets/:budgetId/cut-records/:date */
export async function upsertCutRecord(
  budgetId: string,
  date: string,
  payload: UpsertCutRecordDto,
): Promise<void> {
  await http.put(`${base(budgetId)}/${date}`, payload)
}

/** DELETE /api/budgets/:budgetId/cut-records/:date → 204 */
export async function deleteCutRecord(budgetId: string, date: string): Promise<void> {
  await http.delete(`${base(budgetId)}/${date}`)
}

/** GET /api/budgets/:budgetId/cut-records/dates */
export async function listCutDates(budgetId: string): Promise<string[]> {
  const { data } = await http.get<string[]>(`${base(budgetId)}/dates`)
  return data
}
