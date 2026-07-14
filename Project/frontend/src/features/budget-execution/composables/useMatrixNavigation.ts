import { computed } from 'vue'
import { useBudgetMatrixStore } from '../store'

/**
 * Derived navigation state for the budget matrix period window.
 * Computes the currently visible 3-period slice and boundary guards.
 */
export function useMatrixNavigation(store: ReturnType<typeof useBudgetMatrixStore>) {
  const visiblePeriods = computed(() =>
    store.allPeriods.slice(
      store.visiblePeriodOffset,
      store.visiblePeriodOffset + store.visibleWindowSize,
    ),
  )

  const canGoPrev = computed(() => store.visiblePeriodOffset > 0)

  const canGoNext = computed(
    () => store.visiblePeriodOffset + store.visibleWindowSize < store.allPeriods.length,
  )

  function goPrev(): void {
    store.navigatePrev()
  }

  function goNext(): void {
    store.navigateNext()
  }

  return { visiblePeriods, canGoPrev, canGoNext, goPrev, goNext }
}
