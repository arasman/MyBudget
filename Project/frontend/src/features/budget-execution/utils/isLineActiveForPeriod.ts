import type { BudgetLineResponse, PeriodSummary } from '@/features/budget-structure/types'

/**
 * Returns true when a BudgetLine is active for the given period.
 * Active = line.startDate <= period.startDate AND (endDate == null OR endDate >= period.startDate)
 *
 * REQ-BL-MATRIX-1, AD-5 (Design decision #8: client-side active-for-period filtering)
 */
export function isLineActiveForPeriod(line: BudgetLineResponse, period: PeriodSummary): boolean {
  const lineStart = new Date(line.startDate)
  const periodStart = new Date(period.startDate)
  const lineEnd = line.endDate ? new Date(line.endDate) : null
  return lineStart <= periodStart && (lineEnd === null || lineEnd >= periodStart)
}
