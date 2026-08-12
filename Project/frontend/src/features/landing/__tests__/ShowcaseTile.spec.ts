// jsdom ships no matchMedia — ShowcaseTile itself never calls it (it is a
// pure presentational component per design.md decision #1), so no stub is
// needed here; FlowShowcase.spec.ts and LandingView.spec.ts stub it instead.
import { describe, it, expect } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { createI18n } from 'vue-i18n'
import ShowcaseTile from '../components/ShowcaseTile.vue'
import type { ShowcaseItem } from '../config/showcase'

const ITEM: ShowcaseItem = {
  slug: 'dashboard',
  source: 'dashboard/01-lifetime-trend.png',
  i18nKey: 'landing.showcase.dashboard',
}

function makeI18n() {
  return createI18n({
    legacy: false,
    locale: 'en',
    messages: {
      en: {
        landing: {
          showcase: {
            dashboard: { title: 'See the trend', caption: 'Charts turn numbers into a story.' },
            enlarge: 'Enlarge {title}',
            dismissHint: 'Press Escape or click outside to close',
          },
        },
      },
    },
  })
}

function renderTile(
  props: Partial<{
    item: ShowcaseItem
    active: boolean
    dimmed: boolean
    zoomVars: Record<string, string>
  }> = {},
) {
  return render(ShowcaseTile, {
    props: { item: ITEM, ...props },
    global: { plugins: [makeI18n()] },
  })
}

describe('ShowcaseTile', () => {
  it('idle render: root is a <button> with aria-label from landing.showcase.enlarge, figure markup unchanged', () => {
    const { container } = renderTile()

    const button = screen.getByTestId('showcase-tile')
    expect(button.tagName).toBe('BUTTON')
    expect(button.getAttribute('aria-label')).toBe('Enlarge See the trend')

    const figure = container.querySelector('figure')
    expect(figure).toBeTruthy()
    expect(figure?.querySelector('picture')).toBeTruthy()
    expect(figure?.querySelector('figcaption')).toBeTruthy()
  })

  it('active prop adds the showcase-zoom-card class and applies zoomVars as inline custom properties; figcaption stays visible', () => {
    renderTile({ active: true, zoomVars: { '--zoom-col': '1', '--zoom-cols': '3' } })

    const button = screen.getByTestId('showcase-tile')
    expect(button.className).toContain('showcase-zoom-card')
    expect(button.style.getPropertyValue('--zoom-col')).toBe('1')
    expect(button.style.getPropertyValue('--zoom-cols')).toBe('3')
    expect(button.querySelector('figcaption')).toBeTruthy()
  })

  it('idle (non-active) tile never carries showcase-zoom-card or zoom custom properties', () => {
    renderTile()

    const button = screen.getByTestId('showcase-tile')
    expect(button.className).not.toContain('showcase-zoom-card')
    expect(button.style.getPropertyValue('--zoom-col')).toBe('')
  })

  it('dimmed prop sets inert and aria-hidden="true" on the root', () => {
    renderTile({ dimmed: true })

    const button = screen.getByTestId('showcase-tile')
    expect(button.hasAttribute('inert')).toBe(true)
    expect(button.getAttribute('aria-hidden')).toBe('true')
  })

  it('idle tile carries neither inert nor aria-hidden', () => {
    renderTile()

    const button = screen.getByTestId('showcase-tile')
    expect(button.hasAttribute('inert')).toBe(false)
    expect(button.hasAttribute('aria-hidden')).toBe(false)
  })

  it('mouseenter/mouseleave emit hover-in/hover-out with the tile slug', async () => {
    const { emitted } = renderTile()
    const button = screen.getByTestId('showcase-tile')

    await fireEvent.mouseEnter(button)
    await fireEvent.mouseLeave(button)

    expect(emitted()['hover-in']?.[0]).toEqual(['dashboard'])
    expect(emitted()['hover-out']?.[0]).toEqual([])
  })

  it('click on the button emits activate with the tile slug (native <button> also fires click for Enter/Space)', async () => {
    const { emitted } = renderTile()
    const button = screen.getByTestId('showcase-tile')

    await fireEvent.click(button)

    expect(emitted()['activate']?.[0]).toEqual(['dashboard'])
  })

  // LANDING-9 scenario "Keyboard focus enlarges immediately, no dwell":
  // Tabbing to the tile only focuses it (no Enter/click) — design.md's Data
  // Flow diagram lists `focus` as its own activate trigger alongside
  // click/Enter/Space, so this must not rely on the native click emission.
  it('focus on the button emits activate with the tile slug (WCAG 1.4.13: Tab alone enlarges, no dwell)', async () => {
    const { emitted } = renderTile()
    const button = screen.getByTestId('showcase-tile')

    await fireEvent.focus(button)

    expect(emitted()['activate']?.[0]).toEqual(['dashboard'])
  })
})
