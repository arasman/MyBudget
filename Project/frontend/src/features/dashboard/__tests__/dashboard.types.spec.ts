import { describe, it, expect } from 'vitest'
import {
  LifetimeCutTotalsResponseSchema,
  CutTotalsBandResponseSchema,
  BudgetLineSeriesResponseSchema,
  TOTAL_KEYS,
  type TotalKey,
} from '../types/dashboard'

const makeConceptTotals = (): Record<TotalKey, number> =>
  Object.fromEntries(TOTAL_KEYS.map((key) => [key, 0])) as Record<TotalKey, number>

const makeBand = () =>
  Object.fromEntries(TOTAL_KEYS.map((key) => [key, { avg: 0, min: 0, max: 0 }]))

describe('LifetimeCutTotalsResponseSchema (DASH-1)', () => {
  it('parses a valid response with a full 16-total point', () => {
    const payload = {
      conversionBasis: 'cut-frozen',
      points: [{ cutDate: '2026-01-15', exchangeRate: 7.8, ...makeConceptTotals() }],
    }

    expect(() => LifetimeCutTotalsResponseSchema.parse(payload)).not.toThrow()
  })

  it('parses an empty series for a budget with zero cuts', () => {
    const payload = { conversionBasis: 'cut-frozen', points: [] }

    const result = LifetimeCutTotalsResponseSchema.parse(payload)

    expect(result.points).toEqual([])
  })

  it('rejects a response carrying the wrong conversionBasis literal', () => {
    const payload = { conversionBasis: 'transaction-time', points: [] }

    expect(() => LifetimeCutTotalsResponseSchema.parse(payload)).toThrow()
  })

  it('rejects a point missing one of the 16 total concepts', () => {
    const concepts = makeConceptTotals()
    delete (concepts as Partial<typeof concepts>).totalNet
    const incompletePoint = { cutDate: '2026-01-15', exchangeRate: 7.8, ...concepts }
    const payload = { conversionBasis: 'cut-frozen', points: [incompletePoint] }

    expect(() => LifetimeCutTotalsResponseSchema.parse(payload)).toThrow()
  })
})

describe('CutTotalsBandResponseSchema (DASH-2/3/11)', () => {
  it('parses a valid band with periods and a positive periodCount', () => {
    const payload = {
      conversionBasis: 'cut-frozen',
      periodCount: 2,
      periods: [
        {
          periodId: 'p1',
          periodStart: '2026-01-01',
          periodEnd: '2026-01-31',
          avg: makeConceptTotals(),
        },
      ],
      band: makeBand(),
    }

    expect(() => CutTotalsBandResponseSchema.parse(payload)).not.toThrow()
  })

  it('parses periodCount 0 as the insufficient-history shape (DASH-3)', () => {
    const payload = { conversionBasis: 'cut-frozen', periodCount: 0, periods: [], band: makeBand() }

    const result = CutTotalsBandResponseSchema.parse(payload)

    expect(result.periodCount).toBe(0)
  })

  it('rejects a negative periodCount', () => {
    const payload = { conversionBasis: 'cut-frozen', periodCount: -1, periods: [], band: makeBand() }

    expect(() => CutTotalsBandResponseSchema.parse(payload)).toThrow()
  })

  it('rejects a band value missing min/max', () => {
    const payload = {
      conversionBasis: 'cut-frozen',
      periodCount: 1,
      periods: [],
      band: { ...makeBand(), totalNet: { avg: 0 } },
    }

    expect(() => CutTotalsBandResponseSchema.parse(payload)).toThrow()
  })
})

describe('BudgetLineSeriesResponseSchema (DASH-4/5/6/12)', () => {
  it('parses a valid response with defaultCurrencyId carried per period (DASH-12)', () => {
    const payload = {
      conversionBasis: 'transaction-time',
      periods: [{ periodId: 'p1', cycleId: 'c1', periodStart: '2026-01-01', defaultCurrencyId: 'GTQ' }],
      rows: [{ budgetLineId: 'l1', budgetLineName: 'Rent', periodId: 'p1', budgetedAmount: 100, netTotal: 90 }],
    }

    expect(() => BudgetLineSeriesResponseSchema.parse(payload)).not.toThrow()
  })

  it('parses the empty-selection shape', () => {
    const payload = { conversionBasis: 'transaction-time', periods: [], rows: [] }

    const result = BudgetLineSeriesResponseSchema.parse(payload)

    expect(result.rows).toEqual([])
  })

  it('rejects a period missing defaultCurrencyId (DASH-12 contract)', () => {
    const payload = {
      conversionBasis: 'transaction-time',
      periods: [{ periodId: 'p1', cycleId: 'c1', periodStart: '2026-01-01' }],
      rows: [],
    }

    expect(() => BudgetLineSeriesResponseSchema.parse(payload)).toThrow()
  })

  it('rejects the wrong conversionBasis literal for this endpoint', () => {
    const payload = { conversionBasis: 'cut-frozen', periods: [], rows: [] }

    expect(() => BudgetLineSeriesResponseSchema.parse(payload)).toThrow()
  })
})
