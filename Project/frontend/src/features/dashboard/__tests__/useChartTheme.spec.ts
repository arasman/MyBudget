import { describe, it, expect, vi, afterEach } from 'vitest'
import { useChartTheme } from '../composables/useChartTheme'

describe('useChartTheme', () => {
  let originalGetComputedStyle: typeof window.getComputedStyle

  afterEach(() => {
    if (originalGetComputedStyle) {
      window.getComputedStyle = originalGetComputedStyle
    }
    vi.restoreAllMocks()
  })

  function mockCssVars(vars: Record<string, string>) {
    originalGetComputedStyle = window.getComputedStyle
    window.getComputedStyle = vi.fn(
      () =>
        ({
          getPropertyValue: (name: string) => vars[name] ?? '',
        }) as CSSStyleDeclaration,
    )
  }

  it('reads DaisyUI CSS variables into text/grid colors and a palette', () => {
    mockCssVars({
      '--color-base-content': '#111111',
      '--color-base-300': '#dddddd',
      '--color-primary': '#3b82f6',
      '--color-secondary': '#f472b6',
    })

    const { theme } = useChartTheme()

    expect(theme.value.textColor).toBe('#111111')
    expect(theme.value.gridColor).toBe('#dddddd')
    expect(theme.value.palette[0]).toBe('#3b82f6')
    expect(theme.value.palette[1]).toBe('#f472b6')
  })

  it('falls back to non-empty defaults when a CSS variable is missing', () => {
    mockCssVars({})

    const { theme } = useChartTheme()

    expect(theme.value.textColor.length).toBeGreaterThan(0)
    expect(theme.value.gridColor.length).toBeGreaterThan(0)
    expect(theme.value.palette.length).toBeGreaterThan(0)
  })

  it('refresh() re-reads the current CSS variables (theme switch)', () => {
    mockCssVars({ '--color-base-content': '#111111' })
    const { theme, refresh } = useChartTheme()
    expect(theme.value.textColor).toBe('#111111')

    mockCssVars({ '--color-base-content': '#222222' })
    refresh()

    expect(theme.value.textColor).toBe('#222222')
  })
})
