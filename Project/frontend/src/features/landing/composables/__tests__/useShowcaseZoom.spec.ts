// jsdom ships no matchMedia (design.md Testing Strategy) — every spec that
// touches useShowcaseZoom/ShowcaseTile/FlowShowcase must stub it itself; no
// global setup file exists in vitest.config.ts by design.
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { useShowcaseZoom } from '../useShowcaseZoom'
import type { ShowcaseItem } from '../../config/showcase'

const ITEMS: ShowcaseItem[] = ['a', 'b', 'c', 'd', 'e'].map((slug) => ({
  slug,
  source: `${slug}.png`,
  i18nKey: `landing.showcase.${slug}`,
}))

interface MockMediaQueryList {
  matches: boolean
  media: string
  addEventListener: ReturnType<typeof vi.fn>
  removeEventListener: ReturnType<typeof vi.fn>
  __fireChange(matches: boolean): void
}

function stubMatchMedia(initial: Record<string, boolean>): Map<string, MockMediaQueryList> {
  const lists = new Map<string, MockMediaQueryList>()

  const matchMediaMock = vi.fn((query: string) => {
    const existing = lists.get(query)
    if (existing) return existing

    let changeListener: ((event: { matches: boolean }) => void) | null = null
    const list: MockMediaQueryList = {
      matches: initial[query] ?? false,
      media: query,
      addEventListener: vi.fn((event: string, listener: (event: { matches: boolean }) => void) => {
        if (event === 'change') changeListener = listener
      }),
      removeEventListener: vi.fn(),
      __fireChange(matches: boolean) {
        list.matches = matches
        changeListener?.({ matches })
      },
    }
    lists.set(query, list)
    return list
  })

  vi.stubGlobal('matchMedia', matchMediaMock)
  return lists
}

describe('useShowcaseZoom', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('hoverIn sets activeSlug only after the 175ms dwell (LANDING-9: dwell hover)', () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { activeSlug, hoverIn } = useShowcaseZoom(ITEMS)

    hoverIn('a')
    expect(activeSlug.value).toBeNull()

    vi.advanceTimersByTime(174)
    expect(activeSlug.value).toBeNull()

    vi.advanceTimersByTime(1)
    expect(activeSlug.value).toBe('a')
  })

  it('hoverOut before the dwell elapses cancels it (LANDING-9: sweep without dwell)', () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { activeSlug, hoverIn, hoverOut } = useShowcaseZoom(ITEMS)

    hoverIn('a')
    vi.advanceTimersByTime(100)
    hoverOut()
    vi.advanceTimersByTime(200)

    expect(activeSlug.value).toBeNull()
  })

  it('activateNow sets activeSlug synchronously (LANDING-9: keyboard focus / click enlarge immediately)', () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { activeSlug, activateNow } = useShowcaseZoom(ITEMS)

    activateNow('b')

    expect(activeSlug.value).toBe('b')
  })

  it('deactivate clears activeSlug from any active state (LANDING-9: tap-outside / Escape dismiss)', () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { activeSlug, activateNow, deactivate } = useShowcaseZoom(ITEMS)

    activateNow('c')
    expect(activeSlug.value).toBe('c')

    deactivate()

    expect(activeSlug.value).toBeNull()
  })

  it('is disabled below the sm: breakpoint — hoverIn/activateNow stay no-ops (LANDING-9: disabled below sm:)', () => {
    stubMatchMedia({ '(min-width: 640px)': false, '(min-width: 1024px)': false })
    const { activeSlug, isEnabled, hoverIn, activateNow } = useShowcaseZoom(ITEMS)

    expect(isEnabled.value).toBe(false)

    hoverIn('a')
    vi.advanceTimersByTime(500)
    activateNow('a')

    expect(activeSlug.value).toBeNull()
  })

  it('is enabled at/above the sm: breakpoint', () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { isEnabled } = useShowcaseZoom(ITEMS)

    expect(isEnabled.value).toBe(true)
  })

  it('clears activeSlug when the sm: breakpoint change fires matches: false (design decision 2)', () => {
    const lists = stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { activeSlug, activateNow } = useShowcaseZoom(ITEMS)

    activateNow('a')
    expect(activeSlug.value).toBe('a')

    lists.get('(min-width: 640px)')?.__fireChange(false)

    expect(activeSlug.value).toBeNull()
  })

  it('zoomVars(index) derives --zoom-col/--zoom-cols from the 3-column layout at lg', () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { zoomVars } = useShowcaseZoom(ITEMS)

    expect(zoomVars(0)).toEqual({ '--zoom-col': '0', '--zoom-cols': '3' })
    expect(zoomVars(4)).toEqual({ '--zoom-col': '1', '--zoom-cols': '3' })
  })
})
