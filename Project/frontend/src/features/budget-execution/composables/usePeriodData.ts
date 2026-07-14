import { useBudgetMatrixStore } from '../store'

/**
 * Orchestrates parallel period data loading.
 * Wraps store.loadPeriodTotals for concurrent fetching of the visible window.
 */
export function usePeriodData(store: ReturnType<typeof useBudgetMatrixStore>) {
  async function loadVisiblePeriods(budgetId: string, periodIds: string[]): Promise<void> {
    // Temporarily set budgetId on store if not already set
    // so that loadPeriodTotals can resolve the API call.
    // In practice, store.budgetId is already set by initMatrix.
    await Promise.all(periodIds.map((id) => store.loadPeriodTotals(id)))
  }

  return { loadVisiblePeriods }
}
