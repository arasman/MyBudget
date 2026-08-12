import { getCurrentInstance, onMounted, onUnmounted, ref, type Ref } from 'vue'
import type { ShowcaseItem } from '../config/showcase'

// LANDING-9 (design.md decision #5): a sweeping pointer needs intent
// filtering, keyboard/click do not.
const DWELL_MS = 175
// LANDING-9 (design.md decision #2): JS is authoritative for the sm: gate;
// Tailwind `sm:` prefixes on active-state classes are defense in depth only.
const SM_QUERY = '(min-width: 640px)'
const LG_QUERY = '(min-width: 1024px)'

export interface UseShowcaseZoom {
  activeSlug: Ref<string | null>
  /** matchMedia('(min-width: 640px)') — authoritative for LANDING-9. */
  isEnabled: Ref<boolean>
  /** 3 at lg, 2 at sm, 1 below (gate off) — feeds zoomVars' column math. */
  columns: Ref<1 | 2 | 3>
  hoverIn(slug: string): void
  hoverOut(): void
  activateNow(slug: string): void
  deactivate(): void
  zoomVars(index: number): Record<string, string>
}

/**
 * Owns the single active-tile state for the landing showcase grid
 * (LANDING-9, design.md decisions 1-6): a 175ms hover-dwell timer, the
 * `sm:`-and-up JS-authoritative gate, and the column count used to derive
 * `--zoom-col`/`--zoom-cols` for FlowShowcase's geometry calc().
 *
 * Mirrors useChartTheme.ts's getCurrentInstance() guard so specs can call
 * this composable directly without mounting a host component.
 */
export function useShowcaseZoom(items: ShowcaseItem[]): UseShowcaseZoom {
  const activeSlug = ref<string | null>(null)
  const isEnabled = ref(false)
  const columns = ref<1 | 2 | 3>(1)

  let dwellTimer: ReturnType<typeof setTimeout> | null = null
  let mqSm: MediaQueryList | null = null
  let mqLg: MediaQueryList | null = null

  function clearDwell(): void {
    if (dwellTimer !== null) {
      clearTimeout(dwellTimer)
      dwellTimer = null
    }
  }

  function isKnownSlug(slug: string): boolean {
    return items.some((item) => item.slug === slug)
  }

  function syncBreakpoints(): void {
    isEnabled.value = mqSm?.matches ?? false
    columns.value = mqLg?.matches ? 3 : mqSm?.matches ? 2 : 1
    // design.md decision #2: resizing across the sm: breakpoint must not
    // leave a stuck active state.
    if (!isEnabled.value) {
      clearDwell()
      activeSlug.value = null
    }
  }

  function hoverIn(slug: string): void {
    if (!isEnabled.value || !isKnownSlug(slug)) return
    clearDwell()
    dwellTimer = setTimeout(() => {
      dwellTimer = null
      activeSlug.value = slug
    }, DWELL_MS)
  }

  function hoverOut(): void {
    // Contract: clears a pending dwell timer only — does not deactivate an
    // already-active tile (that is FlowShowcase's grid-level mouseleave).
    clearDwell()
  }

  function activateNow(slug: string): void {
    if (!isEnabled.value || !isKnownSlug(slug)) return
    clearDwell()
    activeSlug.value = slug
  }

  function deactivate(): void {
    clearDwell()
    activeSlug.value = null
  }

  function zoomVars(index: number): Record<string, string> {
    const cols = columns.value
    return {
      '--zoom-col': String(index % cols),
      '--zoom-cols': String(cols),
    }
  }

  function onDocumentKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') deactivate()
  }

  function onDocumentClick(event: MouseEvent): void {
    if (!activeSlug.value) return
    const target = event.target as HTMLElement | null
    if (target?.closest(`[data-showcase-slug="${activeSlug.value}"]`)) return
    deactivate()
  }

  function setup(): void {
    mqSm = window.matchMedia(SM_QUERY)
    mqLg = window.matchMedia(LG_QUERY)
    syncBreakpoints()
    mqSm.addEventListener('change', syncBreakpoints)
    mqLg.addEventListener('change', syncBreakpoints)
    document.addEventListener('keydown', onDocumentKeydown)
    document.addEventListener('click', onDocumentClick)
  }

  function teardown(): void {
    clearDwell()
    mqSm?.removeEventListener('change', syncBreakpoints)
    mqLg?.removeEventListener('change', syncBreakpoints)
    document.removeEventListener('keydown', onDocumentKeydown)
    document.removeEventListener('click', onDocumentClick)
  }

  // Tests may call this composable directly, outside a host component.
  if (getCurrentInstance()) {
    onMounted(setup)
    onUnmounted(teardown)
  } else {
    setup()
  }

  return { activeSlug, isEnabled, columns, hoverIn, hoverOut, activateNow, deactivate, zoomVars }
}
