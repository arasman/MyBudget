import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'

const { mockChartSetup, mockFetchBand } = vi.hoisted(() => ({
  mockChartSetup: vi.fn(),
  mockFetchBand: vi.fn().mockResolvedValue(undefined),
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
        'dashboard.band.axisLabel': 'Amount',
        'dashboard.band.insufficientData.title': 'Not enough history yet',
        'dashboard.band.insufficientData.description': 'At least 2 periods are needed.',
        'dashboard.seriesPicker.title': 'Series',
        'dashboard.seriesPicker.selectAll': 'Select all',
        'dashboard.seriesPicker.clearAll': 'Clear all',
      }
      if (map[key]) return map[key]
      if (key.startsWith('dashboard.series.')) return key.replace('dashboard.series.', '')
      return key
    },
  }),
}))

function zeroConceptTotals() {
  return {
    totalPositive: 0,
    totalPositiveAlt: 0,
    totalNegative: 0,
    totalNegativeAlt: 0,
    totalDeudaEnCurso: 0,
    totalDeudaEnCursoAlt: 0,
    totalBudgeted: 0,
    totalBudgetedAlt: 0,
    totalRegistered: 0,
    totalRegisteredAlt: 0,
    remaining: 0,
    remainingAlt: 0,
    totalAvailable: 0,
    totalAvailableAlt: 0,
    totalNet: 0,
    totalNetAlt: 0,
  }
}

function zeroBand() {
  const zero = { avg: 0, min: 0, max: 0 }
  return {
    totalPositive: zero,
    totalPositiveAlt: zero,
    totalNegative: zero,
    totalNegativeAlt: zero,
    totalDeudaEnCurso: zero,
    totalDeudaEnCursoAlt: zero,
    totalBudgeted: zero,
    totalBudgetedAlt: zero,
    totalRegistered: zero,
    totalRegisteredAlt: zero,
    remaining: zero,
    remainingAlt: zero,
    totalAvailable: zero,
    totalAvailableAlt: zero,
    totalNet: { avg: 15, min: 5, max: 25 },
    totalNetAlt: zero,
  }
}

const storeState: {
  band: {
    conversionBasis: 'cut-frozen'
    periodCount: number
    periods: Array<{ periodId: string; periodStart: string; periodEnd: string; avg: Record<string, number> }>
    band: Record<string, { avg: number; min: number; max: number }>
  } | null
  bandLoading: boolean
  fetchBand: typeof mockFetchBand
} = {
  band: null,
  bandLoading: false,
  fetchBand: mockFetchBand,
}

vi.mock('../store/useDashboardStore', () => ({
  useDashboardStore: () => storeState,
}))

import TotalsBandChart from '../components/TotalsBandChart.vue'

describe('TotalsBandChart (DASH-2/3/11)', () => {
  beforeEach(() => {
    mockChartSetup.mockClear()
    mockFetchBand.mockClear()
    storeState.band = null
    storeState.bandLoading = false
    window.localStorage.clear()
  })

  it('calls store.fetchBand with the budgetId on mount', () => {
    render(TotalsBandChart, { props: { budgetId: 'budget-1' } })

    expect(mockFetchBand).toHaveBeenCalledWith('budget-1')
  })

  it('renders InsufficientDataState instead of the chart when periodCount is 0', () => {
    storeState.band = { conversionBasis: 'cut-frozen', periodCount: 0, periods: [], band: zeroBand() }

    render(TotalsBandChart, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('Not enough history yet')).not.toBeNull()
    expect(mockChartSetup).not.toHaveBeenCalled()
  })

  it('renders InsufficientDataState instead of the chart when periodCount is 1', () => {
    storeState.band = {
      conversionBasis: 'cut-frozen',
      periodCount: 1,
      periods: [{ periodId: 'p1', periodStart: '2026-01-01', periodEnd: '2026-01-31', avg: { ...zeroConceptTotals(), totalNet: 10 } }],
      band: zeroBand(),
    }

    render(TotalsBandChart, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('Not enough history yet')).not.toBeNull()
    expect(mockChartSetup).not.toHaveBeenCalled()
  })

  it('renders the band chart (not the empty state) when periodCount is 2 or more', () => {
    storeState.band = {
      conversionBasis: 'cut-frozen',
      periodCount: 2,
      periods: [
        { periodId: 'p1', periodStart: '2026-01-01', periodEnd: '2026-01-31', avg: { ...zeroConceptTotals(), totalNet: 10 } },
        { periodId: 'p2', periodStart: '2026-02-01', periodEnd: '2026-02-28', avg: { ...zeroConceptTotals(), totalNet: 20 } },
      ],
      band: zeroBand(),
    }

    render(TotalsBandChart, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('Not enough history yet')).toBeNull()
    expect(mockChartSetup).toHaveBeenCalledTimes(1)
    const props = mockChartSetup.mock.calls[0]![0] as { data: { labels: string[]; datasets: unknown[] } }
    expect(props.data.labels).toEqual(['2026-01-01', '2026-02-01'])
  })

  it('emits 3 datasets (min/max/avg) per default-selected key when periodCount is sufficient', () => {
    storeState.band = {
      conversionBasis: 'cut-frozen',
      periodCount: 2,
      periods: [
        { periodId: 'p1', periodStart: '2026-01-01', periodEnd: '2026-01-31', avg: { ...zeroConceptTotals(), totalNet: 10 } },
        { periodId: 'p2', periodStart: '2026-02-01', periodEnd: '2026-02-28', avg: { ...zeroConceptTotals(), totalNet: 20 } },
      ],
      band: zeroBand(),
    }

    render(TotalsBandChart, { props: { budgetId: 'budget-1' } })

    const props = mockChartSetup.mock.calls[0]![0] as { data: { datasets: unknown[] } }
    expect(props.data.datasets.length % 3).toBe(0)
    expect(props.data.datasets.length).toBeGreaterThan(0)
  })

  it('renders the "cut-frozen rate" DASH-9 caption', () => {
    storeState.band = {
      conversionBasis: 'cut-frozen',
      periodCount: 2,
      periods: [
        { periodId: 'p1', periodStart: '2026-01-01', periodEnd: '2026-01-31', avg: zeroConceptTotals() },
        { periodId: 'p2', periodStart: '2026-02-01', periodEnd: '2026-02-28', avg: zeroConceptTotals() },
      ],
      band: zeroBand(),
    }

    render(TotalsBandChart, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('Cut-frozen rate')).not.toBeNull()
  })
})
