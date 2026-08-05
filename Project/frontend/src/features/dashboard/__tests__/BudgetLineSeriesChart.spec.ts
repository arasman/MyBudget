import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'

const { mockChartSetup, mockFetchLineSeries, mockLoadLines, mockLoadCycles } = vi.hoisted(() => ({
  mockChartSetup: vi.fn(),
  mockFetchLineSeries: vi.fn().mockResolvedValue(undefined),
  mockLoadLines: vi.fn().mockResolvedValue(undefined),
  mockLoadCycles: vi.fn().mockResolvedValue(undefined),
}))

vi.mock('vue-chartjs', () => ({
  Chart: {
    name: 'ChartStub',
    props: ['type', 'data', 'options'],
    setup(props: { type: string; data: unknown; options: unknown }) {
      mockChartSetup(props)
      return () => null
    },
  },
}))

vi.mock('vue-i18n', () => ({
  useI18n: () => ({
    t: (key: string) => {
      const map: Record<string, string> = {
        'dashboard.conversionBasis.cutFrozen': 'Cut-frozen rate',
        'dashboard.conversionBasis.transactionTime': 'Transaction-time rate',
        'dashboard.chart.loading': 'Loading chart...',
        'dashboard.chart.empty': 'No data to display',
        'dashboard.lineSeries.axisLabel': 'Amount',
        'dashboard.linePicker.title': 'Budget Lines',
        'dashboard.linePicker.selectAll': 'Select all',
        'dashboard.linePicker.clearAll': 'Clear all',
        'dashboard.comparisonMode.withinCycle': 'Within cycle',
        'dashboard.comparisonMode.crossCycle': 'Cross cycle',
        'dashboard.comparisonMode.cycleLabel': 'Cycle',
        'dashboard.comparisonMode.periodsLabel': 'Periods',
        'dashboard.comparisonMode.cyclesLabel': 'Cycles',
        'dashboard.currencyMismatch.title': 'Currency mismatch',
        'dashboard.currencyMismatch.description': 'Cannot compare across mismatched currencies.',
      }
      return map[key] ?? key
    },
  }),
}))

const dashboardStoreState: {
  lineSeries: {
    conversionBasis: 'transaction-time'
    periods: Array<{ periodId: string; cycleId: string; periodStart: string; defaultCurrencyId: string }>
    rows: Array<{ budgetLineId: string; budgetLineName: string; periodId: string; budgetedAmount: number; netTotal: number }>
  } | null
  lineSeriesLoading: boolean
  fetchLineSeries: typeof mockFetchLineSeries
} = {
  lineSeries: null,
  lineSeriesLoading: false,
  fetchLineSeries: mockFetchLineSeries,
}

vi.mock('../store/useDashboardStore', () => ({
  useDashboardStore: () => dashboardStoreState,
}))

const structureStoreState: {
  budgetLines: Array<{ id: string; name: string }>
  loadLines: typeof mockLoadLines
} = {
  budgetLines: [
    { id: 'l1', name: 'Groceries' },
    { id: 'l2', name: 'Rent' },
  ],
  loadLines: mockLoadLines,
}

vi.mock('@/features/budget-structure/store', () => ({
  useBudgetStructureStore: () => structureStoreState,
}))

const cycleOptionsState: {
  cycles: Array<{ id: string; name: string; defaultCurrencyId: string; periods: Array<{ id: string; name: string; startDate: string }> }>
  loading: boolean
  load: typeof mockLoadCycles
} = {
  cycles: [
    {
      id: 'c1',
      name: 'Cycle 1',
      defaultCurrencyId: 'usd',
      periods: [
        { id: 'p1', name: 'Period 1', startDate: '2026-01-01' },
        { id: 'p2', name: 'Period 2', startDate: '2026-02-01' },
      ],
    },
  ],
  loading: false,
  load: mockLoadCycles,
}

vi.mock('../composables/useCycleOptions', () => ({
  useCycleOptions: () => cycleOptionsState,
}))

import BudgetLineSeriesChart from '../components/BudgetLineSeriesChart.vue'

describe('BudgetLineSeriesChart (DASH-4/5/6/9/12)', () => {
  beforeEach(() => {
    mockChartSetup.mockClear()
    mockFetchLineSeries.mockClear()
    mockLoadLines.mockClear()
    mockLoadCycles.mockClear()
    dashboardStoreState.lineSeries = null
    dashboardStoreState.lineSeriesLoading = false
    window.localStorage.clear()
  })

  it('loads BudgetLines and Cycles on mount', () => {
    render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

    expect(mockLoadLines).toHaveBeenCalledWith('budget-1')
    expect(mockLoadCycles).toHaveBeenCalledWith('budget-1')
  })

  it('fetches the line series once a BudgetLine and 2+ periods are selected (DASH-4/5)', async () => {
    render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

    await fireEvent.click(screen.getByLabelText('Groceries'))
    await fireEvent.click(screen.getByLabelText('Period 1'))
    await fireEvent.click(screen.getByLabelText('Period 2'))

    expect(mockFetchLineSeries).toHaveBeenCalledWith('budget-1', ['l1'], ['p1', 'p2'])
  })

  it('does not fetch while no BudgetLine is selected yet', async () => {
    render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

    await fireEvent.click(screen.getByLabelText('Period 1'))

    expect(mockFetchLineSeries).not.toHaveBeenCalled()
  })

  it('renders the "transaction-time rate" DASH-9 caption when the chart renders', () => {
    dashboardStoreState.lineSeries = {
      conversionBasis: 'transaction-time',
      periods: [{ periodId: 'p1', cycleId: 'c1', periodStart: '2026-01-01', defaultCurrencyId: 'usd' }],
      rows: [],
    }

    render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('Transaction-time rate')).not.toBeNull()
  })

  it('renders the chart (not the mismatch warning) when every period shares one currency', async () => {
    dashboardStoreState.lineSeries = {
      conversionBasis: 'transaction-time',
      periods: [
        { periodId: 'p1', cycleId: 'c1', periodStart: '2026-01-01', defaultCurrencyId: 'usd' },
        { periodId: 'p2', cycleId: 'c1', periodStart: '2026-02-01', defaultCurrencyId: 'usd' },
      ],
      rows: [{ budgetLineId: 'l1', budgetLineName: 'Groceries', periodId: 'p1', budgetedAmount: 100, netTotal: 80 }],
    }

    render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

    // Drives the picker + mode-switch selection so isEmpty flips false —
    // mirrors real usage: a chart only ever renders once the user has
    // actually picked a BudgetLine and 2+ periods.
    await fireEvent.click(screen.getByLabelText('Groceries'))
    await fireEvent.click(screen.getByLabelText('Period 1'))
    await fireEvent.click(screen.getByLabelText('Period 2'))

    expect(screen.queryByRole('alert')).toBeNull()
    expect(mockChartSetup).toHaveBeenCalledTimes(1)
  })

  it('DASH-12: renders the currency-mismatch warning instead of the chart when periods span 2 currencies', () => {
    dashboardStoreState.lineSeries = {
      conversionBasis: 'transaction-time',
      periods: [
        { periodId: 'p1', cycleId: 'c1', periodStart: '2026-01-01', defaultCurrencyId: 'usd' },
        { periodId: 'p3', cycleId: 'c2', periodStart: '2026-04-01', defaultCurrencyId: 'eur' },
      ],
      rows: [{ budgetLineId: 'l1', budgetLineName: 'Groceries', periodId: 'p1', budgetedAmount: 100, netTotal: 80 }],
    }

    render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

    expect(screen.getByRole('alert')).not.toBeNull()
    expect(screen.queryByText('Currency mismatch')).not.toBeNull()
    expect(mockChartSetup).not.toHaveBeenCalled()
  })

  it('shows the loading state while lineSeriesLoading is true', () => {
    dashboardStoreState.lineSeriesLoading = true

    render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('Loading chart...')).not.toBeNull()
  })

  // DASH-13: this widget is the odd one out among the 3 dashboard charts —
  // LifetimeTotalsChart/TotalsBandChart already persist their picker via
  // useSeriesSelection; these prove BudgetLineSeriesChart's picker (lines +
  // comparison mode + within/cross-cycle state) now does the same, scoped
  // per budgetId so one budget's selection never leaks into another.
  describe('picker persistence (DASH-13)', () => {
    it('restores the selection and re-fetches on remount for the same budgetId', async () => {
      const first = render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

      await fireEvent.click(screen.getByLabelText('Groceries'))
      await fireEvent.click(screen.getByLabelText('Period 1'))
      await fireEvent.click(screen.getByLabelText('Period 2'))

      expect(mockFetchLineSeries).toHaveBeenCalledWith('budget-1', ['l1'], ['p1', 'p2'])
      first.unmount()
      mockFetchLineSeries.mockClear()

      render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

      expect((screen.getByLabelText('Groceries') as HTMLInputElement).checked).toBe(true)
      expect((screen.getByLabelText('Period 1') as HTMLInputElement).checked).toBe(true)
      expect((screen.getByLabelText('Period 2') as HTMLInputElement).checked).toBe(true)
      expect(mockFetchLineSeries).toHaveBeenCalledWith('budget-1', ['l1'], ['p1', 'p2'])
    })

    it('does not leak a selection made under one budgetId into a different budgetId', async () => {
      const first = render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

      await fireEvent.click(screen.getByLabelText('Groceries'))
      await fireEvent.click(screen.getByLabelText('Period 1'))
      await fireEvent.click(screen.getByLabelText('Period 2'))
      first.unmount()
      mockFetchLineSeries.mockClear()

      render(BudgetLineSeriesChart, { props: { budgetId: 'budget-2' } })

      expect((screen.getByLabelText('Groceries') as HTMLInputElement).checked).toBe(false)
      expect((screen.getByLabelText('Period 1') as HTMLInputElement).checked).toBe(false)
      expect(mockFetchLineSeries).not.toHaveBeenCalled()
    })

    it('round-trips: remounting the original budgetId again still restores its own selection', async () => {
      const first = render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

      await fireEvent.click(screen.getByLabelText('Groceries'))
      await fireEvent.click(screen.getByLabelText('Period 1'))
      await fireEvent.click(screen.getByLabelText('Period 2'))
      first.unmount()

      const second = render(BudgetLineSeriesChart, { props: { budgetId: 'budget-2' } })
      second.unmount()
      mockFetchLineSeries.mockClear()

      render(BudgetLineSeriesChart, { props: { budgetId: 'budget-1' } })

      expect((screen.getByLabelText('Groceries') as HTMLInputElement).checked).toBe(true)
      expect((screen.getByLabelText('Period 1') as HTMLInputElement).checked).toBe(true)
      expect((screen.getByLabelText('Period 2') as HTMLInputElement).checked).toBe(true)
      expect(mockFetchLineSeries).toHaveBeenCalledWith('budget-1', ['l1'], ['p1', 'p2'])
    })
  })
})
