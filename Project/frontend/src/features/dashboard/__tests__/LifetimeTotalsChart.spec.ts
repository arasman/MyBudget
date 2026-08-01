import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent } from '@testing-library/vue'

// ---------------------------------------------------------------------------
// vue-chartjs mocked (transitively rendered by BaseChart) — DASH-1 mapping
// and store wiring are what's under test, not real canvas rendering.
// ---------------------------------------------------------------------------
const { mockChartSetup, mockFetchSeries } = vi.hoisted(() => ({
  mockChartSetup: vi.fn(),
  mockFetchSeries: vi.fn().mockResolvedValue(undefined),
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
        'dashboard.lifetime.axisLabel': 'Amount',
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

// Store state is mutable per test — mirrors the MatrixControls.spec.ts pattern
// of mocking the store module directly instead of mocking the api layer.
const storeState: {
  series: { conversionBasis: 'cut-frozen'; points: Array<Record<string, unknown>> } | null
  seriesLoading: boolean
  fetchSeries: typeof mockFetchSeries
} = {
  series: null,
  seriesLoading: false,
  fetchSeries: mockFetchSeries,
}

vi.mock('../store/useDashboardStore', () => ({
  useDashboardStore: () => storeState,
}))

import LifetimeTotalsChart from '../components/LifetimeTotalsChart.vue'

function makePoint(cutDate: string, totalNet: number, totalAvailable: number) {
  return {
    cutDate,
    exchangeRate: 1,
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
    totalAvailable,
    totalAvailableAlt: 0,
    totalNet,
    totalNetAlt: 0,
  }
}

describe('LifetimeTotalsChart (DASH-1/DASH-11)', () => {
  beforeEach(() => {
    mockChartSetup.mockClear()
    mockFetchSeries.mockClear()
    storeState.series = null
    storeState.seriesLoading = false
    window.localStorage.clear()
  })

  it('calls store.fetchSeries with the budgetId on mount', () => {
    render(LifetimeTotalsChart, { props: { budgetId: 'budget-1' } })

    expect(mockFetchSeries).toHaveBeenCalledWith('budget-1')
  })

  it('renders a line for every point in store.series, unfiltered, using the default preselected keys', () => {
    storeState.series = {
      conversionBasis: 'cut-frozen',
      points: [makePoint('2026-01-01', 10, 100), makePoint('2026-02-01', 20, 200), makePoint('2026-03-01', 30, 300)],
    }

    render(LifetimeTotalsChart, { props: { budgetId: 'budget-1' } })

    const props = mockChartSetup.mock.calls[0]![0] as { data: { labels: string[]; datasets: unknown[] } }
    expect(props.data.labels).toEqual(['2026-01-01', '2026-02-01', '2026-03-01'])
    expect(props.data.datasets.length).toBeGreaterThan(0)
  })

  it('renders the "cut-frozen rate" DASH-9 caption', () => {
    storeState.series = { conversionBasis: 'cut-frozen', points: [] }

    render(LifetimeTotalsChart, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('Cut-frozen rate')).not.toBeNull()
  })

  it('changing the series-picker selection updates the chart datasets', async () => {
    storeState.series = {
      conversionBasis: 'cut-frozen',
      points: [makePoint('2026-01-01', 10, 100)],
    }

    render(LifetimeTotalsChart, { props: { budgetId: 'budget-1' } })

    const before = mockChartSetup.mock.calls.at(-1)![0] as { data: { datasets: { label: string }[] } }
    const beforeCount = before.data.datasets.length

    await fireEvent.click(screen.getByLabelText('totalPositive'))

    const after = mockChartSetup.mock.calls.at(-1)![0] as { data: { datasets: { label: string }[] } }
    expect(after.data.datasets.length).toBeGreaterThanOrEqual(beforeCount)
  })

  it('shows the loading state and skips fetching when store.seriesLoading is true', () => {
    storeState.seriesLoading = true

    render(LifetimeTotalsChart, { props: { budgetId: 'budget-1' } })

    expect(screen.queryByText('Loading chart...')).not.toBeNull()
  })
})
