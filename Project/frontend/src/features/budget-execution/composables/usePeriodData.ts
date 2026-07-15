import { useBudgetMatrixStore } from '../store'

/**
 * Orchestrates parallel period data loading.
 * Wraps store.loadPeriodTotals for concurrent fetching of the visible window.
 */
export function usePeriodData(store: ReturnType<typeof useBudgetMatrixStore>) {
  async function loadVisiblePeriods(periodIds: string[]): Promise<void> {
    await Promise.all(periodIds.map((id) => store.loadPeriodTotals(id)))
  }

  return { loadVisiblePeriods }
}
