import { ref, watch } from 'vue'
import { TOTAL_KEYS, type TotalKey } from '../types/dashboard'

/**
 * DASH-2/DASH-7: sensible headline default for a first-time visitor —
 * net position + what's still available. Any user pick overrides this via
 * localStorage from then on.
 */
const BUILT_IN_DEFAULT: TotalKey[] = ['totalNet', 'totalAvailable']

function isTotalKey(value: unknown): value is TotalKey {
  return typeof value === 'string' && (TOTAL_KEYS as readonly string[]).includes(value)
}

function readStoredSelection(storageKey: string): TotalKey[] | null {
  if (typeof window === 'undefined') return null
  try {
    const raw = window.localStorage.getItem(storageKey)
    if (!raw) return null
    const parsed: unknown = JSON.parse(raw)
    if (!Array.isArray(parsed)) return null
    const valid = parsed.filter(isTotalKey)
    return valid.length > 0 ? valid : null
  } catch {
    return null
  }
}

/**
 * DASH-2/DASH-7: series-picker selection state for one chart (lifetime or
 * band). `storageKey` MUST be unique per chart instance so the two widgets
 * keep independent selections.
 */
export function useSeriesSelection(storageKey: string, defaultSelection: TotalKey[] = BUILT_IN_DEFAULT) {
  const selected = ref<TotalKey[]>(readStoredSelection(storageKey) ?? [...defaultSelection])

  // `flush: 'sync'` so setSelected() persists immediately — callers (e.g.
  // SeriesPicker's v-model) expect localStorage to reflect the selection
  // without waiting for a Vue tick.
  watch(
    selected,
    (value) => {
      if (typeof window === 'undefined') return
      window.localStorage.setItem(storageKey, JSON.stringify(value))
    },
    { flush: 'sync' },
  )

  function setSelected(keys: TotalKey[]): void {
    selected.value = keys
  }

  return { selected, setSelected }
}
