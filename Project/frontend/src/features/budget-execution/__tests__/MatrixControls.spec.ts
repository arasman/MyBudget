import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'
import { createPinia, setActivePinia } from 'pinia'

// ---------------------------------------------------------------------------
// Hoisted mock factories
// ---------------------------------------------------------------------------

const {
  mockLoadCycleDetail,
  mockUpdateCycle,
  mockSyncExchangeRate,
  mockSetDisplayCurrency,
  mockSetShowDeleted,
} = vi.hoisted(() => ({
  mockLoadCycleDetail: vi.fn().mockResolvedValue(undefined),
  mockUpdateCycle: vi.fn().mockResolvedValue(undefined),
  mockSyncExchangeRate: vi.fn(),
  mockSetDisplayCurrency: vi.fn(),
  mockSetShowDeleted: vi.fn(),
}))

// Mutable shared state for matrixStore
const matrixState = {
  displayCurrency: 'default' as 'default' | 'alternate',
  exchangeRate: 7.5 as number | null,
  alternateCurrencyId: 'usd-id' as string | null,
  budgetId: 'budget-1' as string | null,
  cycleId: 'cycle-1' as string | null,
  showDeleted: false,
  allPeriods: [
    { id: 'p1', name: 'Jan', periodNumber: 1, startDate: '2026-01-01' as never, endDate: '2026-01-31' as never, isClosed: false },
  ],
  setDisplayCurrency: mockSetDisplayCurrency,
  setShowDeleted: mockSetShowDeleted,
  syncExchangeRate: mockSyncExchangeRate,
}

// Mutable shared state for structureStore
const structureState = {
  currentCycle: {
    id: 'cycle-1',
    name: 'Budget 2026',
    startDate: '2026-01-01' as never,
    endDate: '2026-12-31' as never,
    isActive: true,
    exchangeRate: 7.5,
    defaultCurrency: { id: 'gtq', code: 'GTQ', symbol: 'Q' },
    alternateCurrency: { id: 'usd', code: 'USD', symbol: '$' },
    alternateCurrencyId: 'usd-id',
  } as Record<string, unknown> | null,
  loadCycleDetail: mockLoadCycleDetail,
  updateCycle: mockUpdateCycle,
}

vi.mock('../store', () => ({
  useBudgetMatrixStore: () => matrixState,
}))

vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: () => structureState,
}))

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (k: string) => k,
  }),
}))

import MatrixControls from '../components/MatrixControls.vue'

function renderControls() {
  const pinia = createPinia()
  setActivePinia(pinia)
  return render(MatrixControls, { global: { plugins: [pinia] } })
}

describe('MatrixControls.vue', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.clearAllMocks()
    mockLoadCycleDetail.mockResolvedValue(undefined)
    mockUpdateCycle.mockResolvedValue(undefined)
    matrixState.displayCurrency = 'default'
    matrixState.exchangeRate = 7.5
    matrixState.allPeriods = [
      { id: 'p1', name: 'Jan', periodNumber: 1, startDate: '2026-01-01' as never, endDate: '2026-01-31' as never, isClosed: false },
    ]
    structureState.currentCycle = {
      id: 'cycle-1',
      name: 'Budget 2026',
      startDate: '2026-01-01' as never,
      endDate: '2026-12-31' as never,
      isActive: true,
      exchangeRate: 7.5,
      defaultCurrency: { id: 'gtq', code: 'GTQ', symbol: 'Q' },
      alternateCurrency: { id: 'usd', code: 'USD', symbol: '$' },
      alternateCurrencyId: 'usd-id',
    }
  })

  // -------------------------------------------------------------------------
  // Visibility
  // -------------------------------------------------------------------------

  it('does NOT render exchange rate input when displayCurrency = "default"', () => {
    matrixState.displayCurrency = 'default'
    renderControls()
    expect(screen.queryByTestId('exchange-rate-input')).toBeNull()
  })

  it('renders exchange rate input when displayCurrency = "alternate"', () => {
    matrixState.displayCurrency = 'alternate'
    renderControls()
    expect(screen.getByTestId('exchange-rate-input')).not.toBeNull()
  })

  // -------------------------------------------------------------------------
  // Readonly based on period status
  // -------------------------------------------------------------------------

  it('input is editable when at least one period is open', () => {
    matrixState.displayCurrency = 'alternate'
    matrixState.allPeriods = [
      { id: 'p1', name: 'Jan', periodNumber: 1, startDate: '2026-01-01' as never, endDate: '2026-01-31' as never, isClosed: false },
    ]
    renderControls()
    const input = screen.getByTestId('exchange-rate-input') as HTMLInputElement
    expect(input.readOnly).toBe(false)
  })

  it('input is readonly when all periods are closed', () => {
    matrixState.displayCurrency = 'alternate'
    matrixState.allPeriods = [
      { id: 'p1', name: 'Jan', periodNumber: 1, startDate: '2026-01-01' as never, endDate: '2026-01-31' as never, isClosed: true },
    ]
    renderControls()
    const input = screen.getByTestId('exchange-rate-input') as HTMLInputElement
    expect(input.readOnly).toBe(true)
  })

  // -------------------------------------------------------------------------
  // Save flow
  // -------------------------------------------------------------------------

  it('blur triggers loadCycleDetail → updateCycle → loadCycleDetail → syncExchangeRate in order', async () => {
    matrixState.displayCurrency = 'alternate'

    const callOrder: string[] = []
    mockLoadCycleDetail.mockImplementation(async () => { callOrder.push('loadCycleDetail') })
    mockUpdateCycle.mockImplementation(async () => { callOrder.push('updateCycle') })
    mockSyncExchangeRate.mockImplementation(() => { callOrder.push('syncExchangeRate') })

    renderControls()

    const input = screen.getByTestId('exchange-rate-input')
    await fireEvent.blur(input)

    // Wait for async save to complete
    await new Promise((r) => setTimeout(r, 0))

    expect(callOrder).toEqual(['loadCycleDetail', 'updateCycle', 'loadCycleDetail', 'syncExchangeRate'])
  })

  it('Enter keydown triggers the same save flow', async () => {
    matrixState.displayCurrency = 'alternate'

    const callOrder: string[] = []
    mockLoadCycleDetail.mockImplementation(async () => { callOrder.push('loadCycleDetail') })
    mockUpdateCycle.mockImplementation(async () => { callOrder.push('updateCycle') })
    mockSyncExchangeRate.mockImplementation(() => { callOrder.push('syncExchangeRate') })

    renderControls()

    const input = screen.getByTestId('exchange-rate-input')
    await fireEvent.keyDown(input, { key: 'Enter', code: 'Enter' })

    await new Promise((r) => setTimeout(r, 0))

    expect(callOrder).toEqual(['loadCycleDetail', 'updateCycle', 'loadCycleDetail', 'syncExchangeRate'])
  })

  it('saveExchangeRate does nothing when budgetId is null', async () => {
    matrixState.displayCurrency = 'alternate'
    matrixState.budgetId = null

    renderControls()

    const input = screen.getByTestId('exchange-rate-input')
    await fireEvent.blur(input)

    await new Promise((r) => setTimeout(r, 0))

    expect(mockLoadCycleDetail).not.toHaveBeenCalled()
    matrixState.budgetId = 'budget-1'
  })

  // -------------------------------------------------------------------------
  // Exchange rate watch sync
  // -------------------------------------------------------------------------

  it('input initializes with matrixStore.exchangeRate value (watch immediate)', () => {
    matrixState.displayCurrency = 'alternate'
    matrixState.exchangeRate = 7.5

    renderControls()

    const input = screen.getByTestId('exchange-rate-input') as HTMLInputElement
    expect(input.value).toBe('7.5')
  })

  // -------------------------------------------------------------------------
  // Decimal string input
  // -------------------------------------------------------------------------

  it('input accepts decimal string "7.5" without resetting to 0', async () => {
    matrixState.displayCurrency = 'alternate'

    renderControls()

    const input = screen.getByTestId('exchange-rate-input') as HTMLInputElement
    await fireEvent.input(input, { target: { value: '7.5' } })

    // After input event the displayed value should remain "7.5"
    expect(input.value).toBe('7.5')
  })

  // -------------------------------------------------------------------------
  // Exchange rate > 0 guard
  // -------------------------------------------------------------------------

  it('does NOT call loadCycleDetail when exchange rate is 0', async () => {
    matrixState.displayCurrency = 'alternate'

    renderControls()

    const input = screen.getByTestId('exchange-rate-input')
    await fireEvent.input(input, { target: { value: '0' } })
    await fireEvent.blur(input)

    await new Promise((r) => setTimeout(r, 0))

    expect(mockLoadCycleDetail).not.toHaveBeenCalled()
  })

  it('does NOT call loadCycleDetail when exchange rate is negative', async () => {
    matrixState.displayCurrency = 'alternate'

    renderControls()

    const input = screen.getByTestId('exchange-rate-input')
    await fireEvent.input(input, { target: { value: '-1' } })
    await fireEvent.blur(input)

    await new Promise((r) => setTimeout(r, 0))

    expect(mockLoadCycleDetail).not.toHaveBeenCalled()
  })

  it('does NOT call loadCycleDetail when exchange rate input is non-numeric', async () => {
    matrixState.displayCurrency = 'alternate'

    renderControls()

    const input = screen.getByTestId('exchange-rate-input')
    await fireEvent.input(input, { target: { value: 'abc' } })
    await fireEvent.blur(input)

    await new Promise((r) => setTimeout(r, 0))

    expect(mockLoadCycleDetail).not.toHaveBeenCalled()
  })
})
