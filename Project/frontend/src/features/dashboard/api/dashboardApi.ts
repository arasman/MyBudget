import http from '@/api/axios'
import {
  LifetimeCutTotalsResponseSchema,
  CutTotalsBandResponseSchema,
  BudgetLineSeriesResponseSchema,
  type LifetimeCutTotalsResponse,
  type CutTotalsBandResponse,
  type BudgetLineSeriesResponse,
} from '../types/dashboard'

const base = (budgetId: string) => `/api/budgets/${budgetId}/dashboard`

/** GET /api/budgets/:budgetId/dashboard/cut-totals-series (DASH-1) */
export async function getLifetimeCutTotalsSeries(budgetId: string): Promise<LifetimeCutTotalsResponse> {
  const { data } = await http.get(`${base(budgetId)}/cut-totals-series`)
  return LifetimeCutTotalsResponseSchema.parse(data)
}

/** GET /api/budgets/:budgetId/dashboard/cut-totals-band (DASH-2/3/11) */
export async function getCutTotalsBand(budgetId: string): Promise<CutTotalsBandResponse> {
  const { data } = await http.get(`${base(budgetId)}/cut-totals-band`)
  return CutTotalsBandResponseSchema.parse(data)
}

/** GET /api/budgets/:budgetId/dashboard/line-series?lineIds=&periodIds= (DASH-4/5/6/12) */
export async function getBudgetLineSeries(
  budgetId: string,
  lineIds: string[],
  periodIds: string[],
): Promise<BudgetLineSeriesResponse> {
  const params = new URLSearchParams()
  lineIds.forEach((id) => params.append('lineIds', id))
  periodIds.forEach((id) => params.append('periodIds', id))
  const { data } = await http.get(`${base(budgetId)}/line-series?${params.toString()}`)
  return BudgetLineSeriesResponseSchema.parse(data)
}
