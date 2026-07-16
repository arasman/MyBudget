import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'
import MatrixCell from '../components/MatrixCell.vue'

vi.mock('vue-i18n', () => ({
  useI18n: () => ({ t: (k: string) => k }),
}))

const { mockFormatAmount, mockMatrixStore, mockStructureStore } = vi.hoisted(() => ({
  mockFormatAmount: vi.fn((amount: number, _symbol: string) => amount.toFixed(2)),
  mockMatrixStore: {
    displayCurrency: 'default' as 'default' | 'alternate',
    exchangeRate: null as number | null,
  },
  mockStructureStore: {
    currentCycle: null as {
      defaultCurrency?: { symbol: string }
      alternateCurrency?: { symbol: string }
    } | null,
  },
}))

vi.mock('../store', () => ({
  useBudgetMatrixStore: () => mockMatrixStore,
}))

vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: () => mockStructureStore,
}))

vi.mock('../composables/useCurrencyDisplay', () => ({
  useCurrencyDisplay: () => ({
    formatAmount: mockFormatAmount,
  }),
}))

describe('MatrixCell.vue', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockFormatAmount.mockImplementation((amount: number, _symbol: string) => amount.toFixed(2))
    mockMatrixStore.displayCurrency = 'default'
    mockMatrixStore.exchangeRate = null
    mockStructureStore.currentCycle = null
  })

  it('emits dblclick event when double-clicked', async () => {
    const { emitted } = render(MatrixCell, {
      props: { amount: 100, loading: false },
    })

    const cell = screen.getByRole('cell')
    await fireEvent.dblClick(cell)

    expect(emitted()['dblclick']).toBeTruthy()
    expect(emitted()['dblclick']).toHaveLength(1)
  })

  it('shows skeleton div when loading=true and hides amount span', () => {
    const { container } = render(MatrixCell, {
      props: { amount: 500, loading: true },
    })

    const skeleton = container.querySelector('.skeleton')
    expect(skeleton).not.toBeNull()

    // span is absent when loading
    const span = container.querySelector('span')
    expect(span).toBeNull()
  })

  it('shows amount span when loading=false and hides skeleton', () => {
    const { container } = render(MatrixCell, {
      props: { amount: 500, loading: false },
    })

    const skeleton = container.querySelector('.skeleton')
    expect(skeleton).toBeNull()

    const span = container.querySelector('span')
    expect(span).not.toBeNull()
    expect(mockFormatAmount).toHaveBeenCalledWith(500, '')
  })

  it('applies opacity-50 and line-through class when deleted=true', () => {
    const { container } = render(MatrixCell, {
      props: { amount: 200, loading: false, deleted: true },
    })

    const td = container.querySelector('td')
    expect(td?.className).toContain('opacity-50')
    expect(td?.className).toContain('line-through')
  })

  it('does NOT apply strikethrough class when deleted=false', () => {
    const { container } = render(MatrixCell, {
      props: { amount: 200, loading: false, deleted: false },
    })

    const td = container.querySelector('td')
    expect(td?.className).not.toContain('line-through')
  })

  // -------------------------------------------------------------------------
  // Currency symbol resolution (Phase 3)
  // -------------------------------------------------------------------------

  it('uses defaultCurrency symbol when displayCurrency = "default"', () => {
    mockMatrixStore.displayCurrency = 'default'
    mockStructureStore.currentCycle = {
      defaultCurrency: { symbol: 'Q' },
      alternateCurrency: { symbol: '$' },
    }

    render(MatrixCell, { props: { amount: 100, loading: false } })

    expect(mockFormatAmount).toHaveBeenCalledWith(100, 'Q')
  })

  it('uses alternateCurrency symbol when displayCurrency = "alternate"', () => {
    mockMatrixStore.displayCurrency = 'alternate'
    mockStructureStore.currentCycle = {
      defaultCurrency: { symbol: 'Q' },
      alternateCurrency: { symbol: '$' },
    }

    render(MatrixCell, { props: { amount: 100, loading: false } })

    expect(mockFormatAmount).toHaveBeenCalledWith(100, '$')
  })

  it('falls back to empty string when currentCycle is null', () => {
    mockMatrixStore.displayCurrency = 'default'
    mockStructureStore.currentCycle = null

    render(MatrixCell, { props: { amount: 100, loading: false } })

    expect(mockFormatAmount).toHaveBeenCalledWith(100, '')
  })
})
