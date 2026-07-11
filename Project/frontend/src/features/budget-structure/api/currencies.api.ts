import http from '@/api/axios'
import type { CurrencyItem } from '../types'

/** GET /api/budgets/:budgetId/currencies */
export async function listCurrencies(budgetId: string): Promise<CurrencyItem[]> {
  const { data } = await http.get<CurrencyItem[]>(`/api/budgets/${budgetId}/currencies`)
  return data
}
