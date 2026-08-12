// LAYOUT-4 (footer visible on public view, including "/") + Decision 7/13:
// LandingView is anonymous-facing, so it wraps its content in the shared
// PublicBackdrop and mounts AppFooter directly (RootGate renders LandingView
// without going through PublicLayout — see design.md Decision 1).
//
// PR 4 (tasks 4.2-4.4) extends this file with the real landing content:
// LANDING-2 (9 showcase tiles), LANDING-3 (primary/secondary CTA),
// LANDING-4 (secondary outbound links), and LANDING-7 (mobile responsive).
//
// LANDING-9 extends it further: FlowShowcase now mounts useShowcaseZoom(),
// which calls window.matchMedia — jsdom ships none, so this file stubs it
// itself (design.md Testing Strategy; no global vitest setup file exists).
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import { createRouter, createMemoryHistory } from 'vue-router'
import { createI18n } from 'vue-i18n'
import LandingView from '../views/LandingView.vue'
import { SHOWCASE_ITEMS } from '../config/showcase'
import { REPO_URL, README_URL, DECK_URL } from '../config/links'

// LandingView mounts LanguageSwitcher directly (RootGate bypasses PublicLayout,
// see design.md Decision 1) — stub its stores like PublicLayout.spec.ts does.
vi.mock('@/api/axios', () => ({
  default: {
    defaults: { headers: { common: {} } },
    patch: vi.fn().mockResolvedValue({ status: 204 }),
  },
}))

vi.mock('@/stores/locale.store', () => ({
  useLocaleStore: vi.fn(() => ({ locale: 'en', setLocale: vi.fn() })),
}))

vi.mock('@/stores/auth.store', () => ({
  useAuthStore: vi.fn(() => ({ isAuthenticated: false })),
}))

function stubMatchMedia(matches: boolean): void {
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({
      matches,
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })),
  )
}

function makeRouter() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      { path: '/', name: 'BudgetSelection', component: { template: '<div />' } },
      { path: '/login', name: 'Login', component: { template: '<div />' } },
      { path: '/register', name: 'Register', component: { template: '<div />' } },
    ],
  })
  return router
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
        common: { appName: 'MyBudget', switchLanguage: 'Switch language' },
        footer: { poweredBy: 'Powered by ARAS Systems' },
        landing: {
          hero: { title: 'Budget with clarity', subtitle: 'See your money story' },
          showcase: {
            ...showcaseMessages,
            enlarge: 'Enlarge {title}',
            dismissHint: 'Press Escape or click outside to close',
          },
          cta: { primary: 'Create your free account', secondary: 'Sign in' },
          links: { github: 'View source on GitHub', readme: 'Read the docs', deck: 'View the presentation' },
        },
      },
    },
  })
}

async function renderLandingView() {
  const router = makeRouter()
  await router.push('/')
  await router.isReady()

  return render(LandingView, { global: { plugins: [router, makeI18n()] } })
}

describe('LandingView', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    stubMatchMedia(true)
  })

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('renders AppFooter for anonymous visitors at "/"', async () => {
    await renderLandingView()

    const currentYear = new Date().getFullYear()
    expect(screen.getByText(`© ${currentYear} · Powered by ARAS Systems`)).toBeTruthy()
  })

  it('renders the landing root alongside the footer', async () => {
    await renderLandingView()

    expect(screen.getByTestId('landing-view')).toBeTruthy()
    expect(screen.getAllByTestId('showcase-tile')).toHaveLength(9)
  })

  it('renders exactly 9 showcase tiles (LANDING-2)', async () => {
    await renderLandingView()

    expect(screen.getAllByTestId('showcase-tile')).toHaveLength(SHOWCASE_ITEMS.length)
    expect(SHOWCASE_ITEMS).toHaveLength(9)
  })

  it('renders a primary /register CTA styled btn-primary and a secondary /login CTA styled btn-ghost (LANDING-3)', async () => {
    const { container } = await renderLandingView()

    const primary = container.querySelector('a[href="/register"]')
    const secondary = container.querySelector('a[href="/login"]')

    expect(primary).toBeTruthy()
    expect(primary?.className).toContain('btn-primary')
    expect(secondary).toBeTruthy()
    expect(secondary?.className).toContain('btn-ghost')
  })

  it('renders GitHub, README, and deck links, visually secondary to the CTA (LANDING-4)', async () => {
    const { container } = await renderLandingView()

    const github = container.querySelector('[data-testid="link-github"]')
    const readme = container.querySelector('[data-testid="link-readme"]')
    const deck = container.querySelector('[data-testid="link-deck"]')

    expect(github?.getAttribute('href')).toBe(REPO_URL)
    expect(readme?.getAttribute('href')).toBe(README_URL)
    expect(deck?.getAttribute('href')).toBe(DECK_URL)

    for (const link of [github, readme, deck]) {
      expect(link?.className).not.toContain('btn-primary')
    }
  })

  it('ShowcaseTile renders a <picture> with srcset, loading="lazy", and explicit width/height', async () => {
    const { container } = await renderLandingView()

    const tiles = container.querySelectorAll('[data-testid="showcase-tile"]')
    expect(tiles.length).toBeGreaterThan(0)

    tiles.forEach((tile) => {
      const picture = tile.querySelector('picture')
      expect(picture).toBeTruthy()

      const source = picture?.querySelector('source')
      expect(source?.getAttribute('srcset')).toBeTruthy()

      const img = picture?.querySelector('img')
      expect(img?.getAttribute('loading')).toBe('lazy')
      expect(img?.getAttribute('width')).toBeTruthy()
      expect(img?.getAttribute('height')).toBeTruthy()
    })
  })

  it('renders showcase and CTAs without a fixed-width element wider than a 375px mobile viewport (LANDING-7)', async () => {
    const { container } = await renderLandingView()

    const overflowingFixedWidthEl = Array.from(container.querySelectorAll<HTMLElement>('*')).find((el) => {
      const inlineWidth = el.style.width
      return inlineWidth.endsWith('px') && parseFloat(inlineWidth) > 375
    })
    expect(overflowingFixedWidthEl).toBeUndefined()

    const grid = container.querySelector('[data-testid="flow-showcase-grid"]')
    expect(grid?.className).toContain('grid-cols-1')

    const ctaSection = container.querySelector('[data-testid="landing-cta"]')
    expect(ctaSection?.querySelectorAll('a').length).toBeGreaterThanOrEqual(2)
  })

  // LANDING-9 regression guard (design's "Unit — regression" test-strategy
  // row, task 3.7): the enlarge state must not duplicate a tile node, and
  // the geometry calc() must never introduce an inline px width — even with
  // a tile forced active, exactly matching the LANDING-7 guard above.
  it('keeps exactly 9 showcase tiles and no inline px width > 375 with a tile forced active (LANDING-9/LANDING-7 regression)', async () => {
    const { container } = await renderLandingView()

    const tiles = container.querySelectorAll('[data-testid="showcase-tile"]')
    await fireEvent.click(tiles[0] as HTMLElement)
    await Promise.resolve()

    expect(container.querySelectorAll('[data-testid="showcase-tile"]')).toHaveLength(9)
    expect(tiles[0]?.className).toContain('showcase-zoom-card')

    const overflowingFixedWidthEl = Array.from(container.querySelectorAll<HTMLElement>('*')).find((el) => {
      const inlineWidth = el.style.width
      return inlineWidth.endsWith('px') && parseFloat(inlineWidth) > 375
    })
    expect(overflowingFixedWidthEl).toBeUndefined()
  })
})
