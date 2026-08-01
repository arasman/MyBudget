import { ref } from 'vue'
import * as cyclesApi from '@/features/budget-structure/api/cycles.api'

export interface PeriodOption {
  id: string
  name: string
  startDate: string
}

export interface CycleOption {
  id: string
  name: string
  /** Sourced from Cycle.DefaultCurrencyId (DASH-12) — used by the currency-mismatch guard. */
  defaultCurrencyId: string
  periods: PeriodOption[]
}

/**
 * Loads every Cycle for a budget plus its full period list, for the
 * within-cycle/cross-cycle comparison picker (DASH-5/DASH-6). Deliberately
 * NOT built on `useBudgetStructureStore` — that store's `currentCycle` /
 * `periods` refs are a single-cycle-at-a-time singleton used across the
 * budget-structure feature; the comparison picker needs periods for
 * MULTIPLE cycles simultaneously, so this composable calls the existing
 * `cycles.api` read endpoints directly instead of duplicating or
 * repurposing shared UI state.
 */
export function useCycleOptions() {
  const cycles = ref<CycleOption[]>([])
  const loading = ref(false)

  async function load(budgetId: string): Promise<void> {
    loading.value = true
    try {
      const list = await cyclesApi.list(budgetId)
      const details = await Promise.all(list.map((c) => cyclesApi.get(budgetId, c.id)))
      cycles.value = details.map((detail, index) => ({
        id: detail.id,
        name: detail.name,
        defaultCurrencyId: list[index]?.defaultCurrency?.id ?? detail.defaultCurrency?.id ?? '',
        periods: detail.periods.map((p) => ({ id: p.id, name: p.name, startDate: p.startDate })),
      }))
    } finally {
      loading.value = false
    }
  }

  return { cycles, loading, load }
}
