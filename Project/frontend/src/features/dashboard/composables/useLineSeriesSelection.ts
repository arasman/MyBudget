import { computed, ref, watch } from 'vue'
import type { ComparisonMode } from '../utils/comparisonResolution'

export interface LineSeriesSelectionState {
  selectedLineIds: string[]
  mode: ComparisonMode
  selectedCycleId: string | null
  withinPeriodIds: string[]
  crossCycleIds: string[]
}

const DEFAULT_STATE: LineSeriesSelectionState = {
  selectedLineIds: [],
  mode: 'within-cycle',
  selectedCycleId: null,
  withinPeriodIds: [],
  crossCycleIds: [],
}

function storageKeyFor(budgetId: string): string {
  return `dashboard.lineSeries.${budgetId}.selection`
}

function isComparisonMode(value: unknown): value is ComparisonMode {
  return value === 'within-cycle' || value === 'cross-cycle'
}

function isStringArray(value: unknown): value is string[] {
  return Array.isArray(value) && value.every((item) => typeof item === 'string')
}

function readStoredState(storageKey: string): LineSeriesSelectionState | null {
  if (typeof window === 'undefined') return null
  try {
    const raw = window.localStorage.getItem(storageKey)
    if (!raw) return null
    const parsed = JSON.parse(raw) as Partial<LineSeriesSelectionState> | null
    if (!parsed || typeof parsed !== 'object') return null
    const { selectedLineIds, mode, selectedCycleId, withinPeriodIds, crossCycleIds } = parsed
    if (!isStringArray(selectedLineIds)) return null
    if (!isComparisonMode(mode)) return null
    if (selectedCycleId !== null && typeof selectedCycleId !== 'string') return null
    if (!isStringArray(withinPeriodIds)) return null
    if (!isStringArray(crossCycleIds)) return null
    return { selectedLineIds, mode, selectedCycleId, withinPeriodIds, crossCycleIds }
  } catch {
    return null
  }
}

/**
 * DASH-13: BudgetLineSeriesChart picker state (selected lines, comparison
 * mode, and the within/cross-cycle picker's own selection), persisted the
 * same way as the other two dashboard widgets via `useSeriesSelection`.
 *
 * Scoped by budgetId — unlike `useSeriesSelection`'s fixed TotalKey enum,
 * the persisted values here are real entity ids (BudgetLine, Cycle, Period)
 * that only exist within one budget. The Dashboard route reuses this
 * component instance across budgetId changes (no remount), so `budgetId`
 * is accepted as a reactive getter and the storage key switches live when
 * it changes, instead of leaking budget A's ids into budget B.
 */
export function useLineSeriesSelection(budgetId: () => string) {
  const storageKey = ref(storageKeyFor(budgetId()))
  const state = ref<LineSeriesSelectionState>(readStoredState(storageKey.value) ?? { ...DEFAULT_STATE })

  // `flush: 'sync'` so every state replacement persists immediately —
  // mirrors useSeriesSelection.ts (callers expect localStorage to reflect
  // the selection without waiting for a Vue tick).
  watch(
    state,
    (value) => {
      if (typeof window === 'undefined') return
      window.localStorage.setItem(storageKey.value, JSON.stringify(value))
    },
    { flush: 'sync' },
  )

  watch(budgetId, (nextBudgetId) => {
    storageKey.value = storageKeyFor(nextBudgetId)
    state.value = readStoredState(storageKey.value) ?? { ...DEFAULT_STATE }
  })

  function slice<K extends keyof LineSeriesSelectionState>(key: K) {
    return computed<LineSeriesSelectionState[K]>({
      get: () => state.value[key],
      set: (value) => {
        state.value = { ...state.value, [key]: value }
      },
    })
  }

  return {
    selectedLineIds: slice('selectedLineIds'),
    mode: slice('mode'),
    selectedCycleId: slice('selectedCycleId'),
    withinPeriodIds: slice('withinPeriodIds'),
    crossCycleIds: slice('crossCycleIds'),
  }
}
