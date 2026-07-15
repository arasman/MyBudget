import http from '@/api/axios'
import type { PeriodTotalsDto } from '../types'

/** GET /api/budgets/:budgetId/periods/:periodId/execution-totals */
export async function getPeriodTotals(
  budgetId: string,
  periodId: string,
): Promise<PeriodTotalsDto> {
  const { data } = await http.get<PeriodTotalsDto>(
    `/api/budgets/${budgetId}/periods/${periodId}/execution-totals`,
  )
  return data
}
