import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

// ---------------------------------------------------------------------------
// Hoist mock references
// ---------------------------------------------------------------------------
const { mockGetLifetimeCutTotalsSeries, mockGetCutTotalsBand, mockGetBudgetLineSeries } = vi.hoisted(() => ({
  mockGetLifetimeCutTotalsSeries: vi.fn(),
  mockGetCutTotalsBand: vi.fn(),
  mockGetBudgetLineSeries: vi.fn(),
}))

vi.mock('@/features/dashboard/api/dashboardApi', () => ({
  getLifetimeCutTotalsSeries: mockGetLifetimeCutTotalsSeries,
  getCutTotalsBand: mockGetCutTotalsBand,
  getBudgetLineSeries: mockGetBudgetLineSeries,
}))

import { useDashboardStore } from '../store/useDashboardStore'
import type {
  LifetimeCutTotalsResponse,
  CutTotalsBandResponse,
  BudgetLineSeriesResponse,
} from '../types/dashboard'

const BUDGET_ID = 'budget-1'

const makeSeries = (): LifetimeCutTotalsResponse => ({
  conversionBasis: 'cut-frozen',
  points: [],
})

const makeBand = (): CutTotalsBandResponse => ({
  conversionBasis: 'cut-frozen',
  periodCount: 0,
  periods: [],
  band: {} as CutTotalsBandResponse['band'],
})

const makeLineSeries = (): BudgetLineSeriesResponse => ({
  conversionBasis: 'transaction-time',
  periods: [],
  rows: [],
})

describe('useDashboardStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.resetAllMocks()
  })

  describe('fetchSeries (DASH-1)', () => {
    it('sets series and clears loading on success', async () => {
      const response = makeSeries()
      mockGetLifetimeCutTotalsSeries.mockResolvedValue(response)

      const store = useDashboardStore()
      const promise = store.fetchSeries(BUDGET_ID)
      expect(store.seriesLoading).toBe(true)
      await promise

      expect(store.series).toEqual(response)
      expect(store.seriesLoading).toBe(false)
      expect(store.seriesError).toBeNull()
    })

    it('sets seriesError on failure and keeps series null', async () => {
      mockGetLifetimeCutTotalsSeries.mockRejectedValue(new Error('network down'))

      const store = useDashboardStore()
      await store.fetchSeries(BUDGET_ID)

      expect(store.seriesError).toBe('network down')
      expect(store.series).toBeNull()
      expect(store.seriesLoading).toBe(false)
    })
  })

  describe('fetchBand (DASH-2/3/11)', () => {
    it('sets band and clears loading on success', async () => {
      const response = makeBand()
      mockGetCutTotalsBand.mockResolvedValue(response)

      const store = useDashboardStore()
      await store.fetchBand(BUDGET_ID)

      expect(store.band).toEqual(response)
      expect(store.bandLoading).toBe(false)
      expect(store.bandError).toBeNull()
    })

    it('sets bandError on failure', async () => {
      mockGetCutTotalsBand.mockRejectedValue(new Error('band failed'))

      const store = useDashboardStore()
      await store.fetchBand(BUDGET_ID)

      expect(store.bandError).toBe('band failed')
      expect(store.band).toBeNull()
    })
  })

  describe('fetchLineSeries (DASH-4/5/6/12)', () => {
    it('sets lineSeries and clears loading on success', async () => {
      const response = makeLineSeries()
      mockGetBudgetLineSeries.mockResolvedValue(response)

      const store = useDashboardStore()
      await store.fetchLineSeries(BUDGET_ID, ['line-1'], ['period-1'])

      expect(mockGetBudgetLineSeries).toHaveBeenCalledWith(BUDGET_ID, ['line-1'], ['period-1'])
      expect(store.lineSeries).toEqual(response)
      expect(store.lineSeriesLoading).toBe(false)
      expect(store.lineSeriesError).toBeNull()
    })

    it('sets lineSeriesError on failure', async () => {
      mockGetBudgetLineSeries.mockRejectedValue(new Error('line series failed'))

      const store = useDashboardStore()
      await store.fetchLineSeries(BUDGET_ID, [], [])

      expect(store.lineSeriesError).toBe('line series failed')
      expect(store.lineSeries).toBeNull()
    })
  })

  describe('reset', () => {
    it('clears all state and error fields', async () => {
      mockGetLifetimeCutTotalsSeries.mockResolvedValue(makeSeries())
      mockGetCutTotalsBand.mockResolvedValue(makeBand())

      const store = useDashboardStore()
      await store.fetchSeries(BUDGET_ID)
      await store.fetchBand(BUDGET_ID)

      store.reset()

      expect(store.series).toBeNull()
      expect(store.band).toBeNull()
      expect(store.lineSeries).toBeNull()
      expect(store.seriesError).toBeNull()
      expect(store.bandError).toBeNull()
      expect(store.lineSeriesError).toBeNull()
    })
  })
})
