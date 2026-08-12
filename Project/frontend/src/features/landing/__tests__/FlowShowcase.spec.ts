// jsdom ships no matchMedia — FlowShowcase mounts useShowcaseZoom(), so every
// test here must stub it itself (design.md Testing Strategy; no global
// vitest setup file exists by design).
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { createI18n } from 'vue-i18n'
import FlowShowcase from '../components/FlowShowcase.vue'
import { SHOWCASE_ITEMS } from '../config/showcase'

interface MockMediaQueryList {
  matches: boolean
  media: string
  addEventListener: ReturnType<typeof vi.fn>
  removeEventListener: ReturnType<typeof vi.fn>
}

function stubMatchMedia(initial: Record<string, boolean>): void {
  const lists = new Map<string, MockMediaQueryList>()

  const matchMediaMock = vi.fn((query: string) => {
    const existing = lists.get(query)
    if (existing) return existing

    const list: MockMediaQueryList = {
      matches: initial[query] ?? false,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    }
    lists.set(query, list)
    return list
  })

  vi.stubGlobal('matchMedia', matchMediaMock)
}

function makeI18n() {
  const showcaseMessages = Object.fromEntries(
    SHOWCASE_ITEMS.map((item) => {
      const key = item.i18nKey.split('.').pop() as string
      return [key, { title: `${key} title`, caption: `${key} caption` }]
    }),
  )

  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        landing: {
          showcase: {
            ...showcaseMessages,
            enlarge: 'Enlarge {title}',
            dismissHint: 'Press Escape or click outside to close',
          },
        },
      },
    },
  })
}

function renderFlowShowcase() {
  return render(FlowShowcase, { global: { plugins: [makeI18n()] } })
}

describe('FlowShowcase', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
    vi.unstubAllGlobals()
  })

  it('hovering tile A then moving to tile B before the dwell fires never activates A (LANDING-9: sweep without dwell)', async () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { container } = renderFlowShowcase()
    const tiles = container.querySelectorAll('[data-testid="showcase-tile"]')
    const [tileA, tileB] = [tiles[0] as HTMLElement, tiles[1] as HTMLElement]

    await fireEvent.mouseEnter(tileA)
    vi.advanceTimersByTime(100)
    await fireEvent.mouseEnter(tileB)
    vi.advanceTimersByTime(100)

    expect(tileA.className).not.toContain('showcase-zoom-card')
    expect(tileB.className).not.toContain('showcase-zoom-card')

    vi.advanceTimersByTime(100)
    await Promise.resolve()

    expect(tileA.className).not.toContain('showcase-zoom-card')
  })

  it('a completed dwell activates exactly one tile and the other 8 receive dimmed=true (LANDING-9: sibling de-emphasis)', async () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { container } = renderFlowShowcase()
    const tiles = Array.from(container.querySelectorAll('[data-testid="showcase-tile"]')) as HTMLElement[]

    await fireEvent.mouseEnter(tiles[0] as HTMLElement)
    vi.advanceTimersByTime(175)
    await Promise.resolve()

    const active = tiles.filter((tile) => tile.className.includes('showcase-zoom-card'))
    expect(active).toHaveLength(1)
    expect(active[0]).toBe(tiles[0])

    const dimmed = tiles.filter((tile) => tile !== tiles[0])
    expect(dimmed).toHaveLength(8)
    dimmed.forEach((tile) => {
      expect(tile.hasAttribute('inert')).toBe(true)
      expect(tile.getAttribute('aria-hidden')).toBe('true')
    })
  })

  it('below the sm: breakpoint no tile ever becomes active regardless of hover/activate (LANDING-9: disabled below sm:)', async () => {
    stubMatchMedia({ '(min-width: 640px)': false, '(min-width: 1024px)': false })
    const { container } = renderFlowShowcase()
    const tiles = Array.from(container.querySelectorAll('[data-testid="showcase-tile"]')) as HTMLElement[]

    await fireEvent.mouseEnter(tiles[0] as HTMLElement)
    vi.advanceTimersByTime(500)
    await fireEvent.click(tiles[0] as HTMLElement)
    await Promise.resolve()

    tiles.forEach((tile) => {
      expect(tile.className).not.toContain('showcase-zoom-card')
      expect(tile.style.getPropertyValue('--zoom-col')).toBe('')
    })
  })

  it('Escape clears the active tile and focus remains on a reachable element (LANDING-9: Escape dismiss)', async () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { container } = renderFlowShowcase()
    const tiles = Array.from(container.querySelectorAll('[data-testid="showcase-tile"]')) as HTMLElement[]

    await fireEvent.click(tiles[0] as HTMLElement)
    await Promise.resolve()
    expect(tiles[0]?.className).toContain('showcase-zoom-card')

    await fireEvent.keyDown(document, { key: 'Escape' })
    await Promise.resolve()

    expect(tiles[0]?.className).not.toContain('showcase-zoom-card')
    expect(document.activeElement).not.toBeNull()
    expect((document.activeElement as HTMLElement).hasAttribute('inert')).toBe(false)
  })

  it('a click outside the active tile clears it (LANDING-9: tap-outside dismiss)', async () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { container } = renderFlowShowcase()
    const tiles = Array.from(container.querySelectorAll('[data-testid="showcase-tile"]')) as HTMLElement[]

    await fireEvent.click(tiles[0] as HTMLElement)
    await Promise.resolve()
    expect(tiles[0]?.className).toContain('showcase-zoom-card')

    await fireEvent.click(document.body)
    await Promise.resolve()

    expect(tiles[0]?.className).not.toContain('showcase-zoom-card')
  })

  it('mouseleave on the grid container (not a single tile) clears the active/pending state (design decision 4)', async () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { container } = renderFlowShowcase()
    const grid = screen.getByTestId('flow-showcase-grid')
    const tiles = Array.from(container.querySelectorAll('[data-testid="showcase-tile"]')) as HTMLElement[]

    await fireEvent.click(tiles[0] as HTMLElement)
    await Promise.resolve()
    expect(tiles[0]?.className).toContain('showcase-zoom-card')

    await fireEvent.mouseLeave(grid)
    await Promise.resolve()

    expect(tiles[0]?.className).not.toContain('showcase-zoom-card')
  })

  it('mouseleave fired on a single tile alone does not clear an already-active tile (design decision 4)', async () => {
    stubMatchMedia({ '(min-width: 640px)': true, '(min-width: 1024px)': true })
    const { container } = renderFlowShowcase()
    const tiles = Array.from(container.querySelectorAll('[data-testid="showcase-tile"]')) as HTMLElement[]

    await fireEvent.click(tiles[0] as HTMLElement)
    await Promise.resolve()
    expect(tiles[0]?.className).toContain('showcase-zoom-card')

    await fireEvent.mouseLeave(tiles[0] as HTMLElement)
    await Promise.resolve()

    expect(tiles[0]?.className).toContain('showcase-zoom-card')
  })
})
