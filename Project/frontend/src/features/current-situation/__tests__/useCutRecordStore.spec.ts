import { describe, it, expect, vi, beforeEach } from 'vitest'
import { setActivePinia, createPinia } from 'pinia'

// ---------------------------------------------------------------------------
// Hoist mock references
// ---------------------------------------------------------------------------
const { mockGetCutRecord, mockUpsertCutRecord, mockDeleteCutRecord, mockListCutDates } =
  vi.hoisted(() => ({
    mockGetCutRecord: vi.fn(),
    mockUpsertCutRecord: vi.fn(),
    mockDeleteCutRecord: vi.fn(),
    mockListCutDates: vi.fn(),
  }))

vi.mock('@/features/current-situation/api/cutRecordApi', () => ({
  getCutRecord: mockGetCutRecord,
  upsertCutRecord: mockUpsertCutRecord,
  deleteCutRecord: mockDeleteCutRecord,
  listCutDates: mockListCutDates,
}))

import { useCutRecordStore } from '../store/useCutRecordStore'
import type { CutRecordResponse } from '../types/cutRecord'

const BUDGET_ID = 'budget-1'

const makeRecord = (overrides: Partial<CutRecordResponse> = {}): CutRecordResponse => ({
  isDraft: false,
  cutRecordId: 'cut-1',
  cutDate: '2026-07-25',
  exchangeRate: 7.8,
  projectionsJson: null,
  primaryCurrencyId: '11111111-1111-1111-1111-111111111111',
  executionSummary: { totalBudgeted: 1000, totalRegistered: 800, remaining: 200 },
  accounts: [],
  totals: {
    totalPositive: 500,
    totalNegative: 200,
    totalDeudaEnCurso: 400,
    totalPositiveAlt: 64.1,
    totalNegativeAlt: 25.64,
    totalDeudaEnCursoAlt: 51.28,
  },
  ...overrides,
})

describe('useCutRecordStore', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.resetAllMocks()
  })

  describe('fetchCutDates', () => {
    it('populates cutDates array', async () => {
      mockListCutDates.mockResolvedValue(['2026-07-20', '2026-07-25', '2026-07-28'])

      const store = useCutRecordStore()
      await store.fetchCutDates(BUDGET_ID)

      expect(store.cutDates).toEqual(['2026-07-20', '2026-07-25', '2026-07-28'])
    })

    it('sets error on failure', async () => {
      mockListCutDates.mockRejectedValue(new Error('API error'))

      const store = useCutRecordStore()
      await store.fetchCutDates(BUDGET_ID)

      expect(store.error).toBe('API error')
    })
  })

  describe('fetchCutRecord', () => {
    it('sets currentRecord and syncs index when date found in list', async () => {
      const record = makeRecord({ cutDate: '2026-07-25' })
      mockListCutDates.mockResolvedValue(['2026-07-20', '2026-07-25', '2026-07-28'])
      mockGetCutRecord.mockResolvedValue(record)

      const store = useCutRecordStore()
      await store.fetchCutDates(BUDGET_ID)
      await store.fetchCutRecord(BUDGET_ID, '2026-07-25')

      expect(store.currentRecord).toEqual(record)
      expect(store.currentDateIndex).toBe(1)
    })

    it('sets index to -1 for draft dates not in list', async () => {
      const record = makeRecord({ isDraft: true, cutDate: '2026-07-30' })
      mockListCutDates.mockResolvedValue(['2026-07-25'])
      mockGetCutRecord.mockResolvedValue(record)

      const store = useCutRecordStore()
      await store.fetchCutDates(BUDGET_ID)
      await store.fetchCutRecord(BUDGET_ID, '2026-07-30')

      expect(store.currentDateIndex).toBe(-1)
    })
  })

  describe('navigation computed', () => {
    it('hasPrevious is false at first date, true otherwise', async () => {
      mockListCutDates.mockResolvedValue(['2026-07-20', '2026-07-25', '2026-07-28'])
      mockGetCutRecord.mockResolvedValue(makeRecord({ cutDate: '2026-07-20' }))

      const store = useCutRecordStore()
      await store.fetchCutDates(BUDGET_ID)
      await store.fetchCutRecord(BUDGET_ID, '2026-07-20')

      expect(store.hasPrevious).toBe(false)
      expect(store.hasNext).toBe(true)
      expect(store.previousDate).toBeNull()
      expect(store.nextDate).toBe('2026-07-25')
    })

    it('hasNext is false at last date, hasPrevious true', async () => {
      mockListCutDates.mockResolvedValue(['2026-07-20', '2026-07-25', '2026-07-28'])
      mockGetCutRecord.mockResolvedValue(makeRecord({ cutDate: '2026-07-28' }))

      const store = useCutRecordStore()
      await store.fetchCutDates(BUDGET_ID)
      await store.fetchCutRecord(BUDGET_ID, '2026-07-28')

      expect(store.hasPrevious).toBe(true)
      expect(store.hasNext).toBe(false)
      expect(store.previousDate).toBe('2026-07-25')
      expect(store.nextDate).toBeNull()
    })

    it('both hasPrevious and hasNext true for middle date', async () => {
      mockListCutDates.mockResolvedValue(['2026-07-20', '2026-07-25', '2026-07-28'])
      mockGetCutRecord.mockResolvedValue(makeRecord({ cutDate: '2026-07-25' }))

      const store = useCutRecordStore()
      await store.fetchCutDates(BUDGET_ID)
      await store.fetchCutRecord(BUDGET_ID, '2026-07-25')

      expect(store.hasPrevious).toBe(true)
      expect(store.hasNext).toBe(true)
    })
  })

  describe('deleteCutRecord', () => {
    it('removes date from cutDates and clears currentRecord', async () => {
      mockListCutDates.mockResolvedValue(['2026-07-20', '2026-07-25'])
      mockGetCutRecord.mockResolvedValue(makeRecord({ cutDate: '2026-07-25' }))
      mockDeleteCutRecord.mockResolvedValue(undefined)

      const store = useCutRecordStore()
      await store.fetchCutDates(BUDGET_ID)
      await store.fetchCutRecord(BUDGET_ID, '2026-07-25')
      await store.deleteCutRecord(BUDGET_ID, '2026-07-25')

      expect(store.cutDates).toEqual(['2026-07-20'])
      expect(store.currentRecord).toBeNull()
    })
  })
})
