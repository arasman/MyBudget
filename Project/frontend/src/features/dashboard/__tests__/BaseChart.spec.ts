import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/vue'

// ---------------------------------------------------------------------------
// vue-chartjs is mocked — BaseChart's prop→dataset mapping is what's under
// test, not real canvas rendering (design.md Testing Strategy: "Chart.js
// mocked").
// ---------------------------------------------------------------------------
const { mockChartSetup } = vi.hoisted(() => ({ mockChartSetup: vi.fn() }))

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
      }
      return map[key] ?? key
    },
  }),
}))

import BaseChart from '../components/BaseChart.vue'

const baseProps = {
  type: 'line' as const,
  series: [{ key: 'totalNet', label: 'Total Net', data: [10, 20, 30] }],
  labels: ['Jan', 'Feb', 'Mar'],
  conversionBasis: 'cut-frozen' as const,
}

describe('BaseChart', () => {
  beforeEach(() => {
    mockChartSetup.mockClear()
  })

  it('maps type/series/labels props into the Chart.js data shape', () => {
    render(BaseChart, { props: baseProps })

    expect(mockChartSetup).toHaveBeenCalledTimes(1)
    const props = mockChartSetup.mock.calls[0][0] as {
      type: string
      data: { labels: string[]; datasets: { label: string; data: number[] }[] }
    }
    expect(props.type).toBe('line')
    expect(props.data.labels).toEqual(['Jan', 'Feb', 'Mar'])
    expect(props.data.datasets).toHaveLength(1)
    expect(props.data.datasets[0]!.label).toBe('Total Net')
    expect(props.data.datasets[0]!.data).toEqual([10, 20, 30])
  })

  it('maps multiple series into multiple datasets', () => {
    render(BaseChart, {
      props: {
        ...baseProps,
        series: [
          { key: 'totalNet', label: 'Total Net', data: [1, 2] },
          { key: 'totalAvailable', label: 'Total Available', data: [3, 4] },
        ],
      },
    })

    const props = mockChartSetup.mock.calls[0][0] as { data: { datasets: unknown[] } }
    expect(props.data.datasets).toHaveLength(2)
  })

  it('renders the "cut-frozen rate" caption for DASH-1/DASH-2 sourced charts (DASH-9)', () => {
    render(BaseChart, { props: baseProps })
    expect(screen.queryByText('Cut-frozen rate')).not.toBeNull()
  })

  it('renders the "transaction-time rate" caption for DASH-4/5/6 sourced charts (DASH-9)', () => {
    render(BaseChart, { props: { ...baseProps, conversionBasis: 'transaction-time' as const } })
    expect(screen.queryByText('Transaction-time rate')).not.toBeNull()
  })

  it('renders a loading state and skips the chart when loading is true', () => {
    render(BaseChart, { props: { ...baseProps, loading: true } })

    expect(mockChartSetup).not.toHaveBeenCalled()
    expect(screen.queryByText('Loading chart...')).not.toBeNull()
  })

  it('renders an empty state and skips the chart when empty is true', () => {
    render(BaseChart, { props: { ...baseProps, empty: true } })

    expect(mockChartSetup).not.toHaveBeenCalled()
    expect(screen.queryByText('No data to display')).not.toBeNull()
  })

  it('still renders the conversion-basis caption while loading', () => {
    render(BaseChart, { props: { ...baseProps, loading: true } })
    expect(screen.queryByText('Cut-frozen rate')).not.toBeNull()
  })
})
