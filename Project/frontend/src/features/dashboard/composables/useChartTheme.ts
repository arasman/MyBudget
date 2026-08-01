import { ref, getCurrentInstance, onMounted, onBeforeUnmount } from 'vue'

export interface ChartTheme {
  textColor: string
  gridColor: string
  palette: string[]
}

// DaisyUI v5 exposes theme colors as CSS custom properties on :root, already
// usable as color values (no oklch() wrapper needed at the call site) — see
// daisyui/colors/properties.css. Reading them here keeps chart colors in
// sync with the active DaisyUI theme without hardcoding a palette.
const TEXT_VAR = '--color-base-content'
const GRID_VAR = '--color-base-300'
const PALETTE_VARS = [
  '--color-primary',
  '--color-secondary',
  '--color-accent',
  '--color-info',
  '--color-success',
  '--color-warning',
  '--color-error',
  '--color-neutral',
]

const FALLBACK_TEXT = '#1f2937'
const FALLBACK_GRID = '#e5e7eb'
const FALLBACK_PALETTE = ['#3b82f6', '#ec4899', '#f59e0b', '#06b6d4', '#22c55e', '#eab308', '#ef4444', '#6b7280']

function readCssVar(name: string, fallback: string): string {
  if (typeof window === 'undefined' || typeof document === 'undefined') return fallback
  const value = window.getComputedStyle(document.documentElement).getPropertyValue(name).trim()
  return value || fallback
}

function buildTheme(): ChartTheme {
  return {
    textColor: readCssVar(TEXT_VAR, FALLBACK_TEXT),
    gridColor: readCssVar(GRID_VAR, FALLBACK_GRID),
    palette: PALETTE_VARS.map((name, index) => readCssVar(name, FALLBACK_PALETTE[index] ?? FALLBACK_TEXT)),
  }
}

/**
 * Maps DaisyUI's active-theme CSS variables to Chart.js-ready colors.
 * Re-reads on `data-theme` attribute changes so charts follow theme
 * switches, even though no theme-toggle UI exists yet (App.vue only sets
 * the initial theme from localStorage today).
 */
export function useChartTheme() {
  const theme = ref<ChartTheme>(buildTheme())

  function refresh(): void {
    theme.value = buildTheme()
  }

  let observer: MutationObserver | null = null

  function startObserving(): void {
    if (typeof MutationObserver === 'undefined' || typeof document === 'undefined') return
    observer = new MutationObserver(refresh)
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] })
  }

  function stopObserving(): void {
    observer?.disconnect()
    observer = null
  }

  // Only register Vue lifecycle hooks when called from within a component
  // setup() — tests may call this composable directly, outside a host
  // component.
  if (getCurrentInstance()) {
    onMounted(startObserving)
    onBeforeUnmount(stopObserving)
  } else {
    startObserving()
  }

  return { theme, refresh }
}
