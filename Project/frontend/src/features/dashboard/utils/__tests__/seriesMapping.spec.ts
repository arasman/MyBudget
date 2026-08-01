import { describe, it, expect } from 'vitest'
import { buildLifetimeSeries, buildBandChartSeries, buildLineSeries, withAlpha } from '../seriesMapping'
import type { CutTotalsPoint, PeriodAverage, TotalsBand, TotalKey, BudgetLineSeriesRow, PeriodSeries } from '../../types/dashboard'

const KEYS: TotalKey[] = ['totalNet', 'totalAvailable']

function labelFor(key: TotalKey): string {
  return `Label:${key}`
}

function makePoint(cutDate: string, totalNet: number, totalAvailable: number): CutTotalsPoint {
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

describe('buildLifetimeSeries (DASH-1)', () => {
  it('maps each selected key into one dataset with data pulled from every point, in point order', () => {
    const points = [makePoint('2026-01-01', 10, 100), makePoint('2026-02-01', 20, 200)]

    const result = buildLifetimeSeries(points, KEYS, labelFor)

    expect(result).toHaveLength(2)
    expect(result[0]).toMatchObject({ key: 'totalNet', label: 'Label:totalNet', data: [10, 20] })
    expect(result[1]).toMatchObject({ key: 'totalAvailable', label: 'Label:totalAvailable', data: [100, 200] })
  })

  it('returns an empty array when no keys are selected, regardless of points', () => {
    const points = [makePoint('2026-01-01', 10, 100)]

    const result = buildLifetimeSeries(points, [], labelFor)

    expect(result).toEqual([])
  })

  it('returns datasets with empty data arrays for a budget with zero cuts', () => {
    const result = buildLifetimeSeries([], KEYS, labelFor)

    expect(result).toHaveLength(2)
    expect(result[0]!.data).toEqual([])
  })
})

describe('withAlpha', () => {
  it('wraps a color in a color-mix() expression at the requested opacity', () => {
    expect(withAlpha('#3b82f6', 0.2)).toBe('color-mix(in srgb, #3b82f6 20%, transparent)')
  })

  it('clamps alpha above 1 down to 100%', () => {
    expect(withAlpha('#3b82f6', 5)).toBe('color-mix(in srgb, #3b82f6 100%, transparent)')
  })

  it('clamps negative alpha up to 0%', () => {
    expect(withAlpha('#3b82f6', -1)).toBe('color-mix(in srgb, #3b82f6 0%, transparent)')
  })
})

describe('buildBandChartSeries (DASH-2/3/11)', () => {
  const makeBandValue = (avg: number, min: number, max: number) => ({ avg, min, max })

  function makeBand(): TotalsBand {
    const zero = makeBandValue(0, 0, 0)
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
      totalAvailable: makeBandValue(150, 100, 200),
      totalAvailableAlt: zero,
      totalNet: makeBandValue(15, 5, 25),
      totalNetAlt: zero,
    }
  }

  function makePeriods(): PeriodAverage[] {
    return [
      {
        periodId: 'p1',
        periodStart: '2026-01-01',
        periodEnd: '2026-01-31',
        avg: { ...zeroConceptTotals(), totalNet: 10, totalAvailable: 120 },
      },
      {
        periodId: 'p2',
        periodStart: '2026-02-01',
        periodEnd: '2026-02-28',
        avg: { ...zeroConceptTotals(), totalNet: 20, totalAvailable: 180 },
      },
    ]
  }

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

  it('emits 3 datasets per selected key: min, max, avg — in that order', () => {
    const result = buildBandChartSeries(makePeriods(), makeBand(), ['totalNet'], labelFor, ['#3b82f6'])

    expect(result).toHaveLength(3)
    expect(result.map((d) => d.key)).toEqual(['totalNet:min', 'totalNet:max', 'totalNet:avg'])
  })

  it('min/max datasets repeat the single aggregate band value across every period', () => {
    const result = buildBandChartSeries(makePeriods(), makeBand(), ['totalNet'], labelFor, ['#3b82f6'])
    const min = result.find((d) => d.key === 'totalNet:min')!
    const max = result.find((d) => d.key === 'totalNet:max')!

    expect(min.data).toEqual([5, 5])
    expect(max.data).toEqual([25, 25])
  })

  it('the avg dataset plots the period-by-period average, not the aggregate', () => {
    const result = buildBandChartSeries(makePeriods(), makeBand(), ['totalNet'], labelFor, ['#3b82f6'])
    const avg = result.find((d) => d.key === 'totalNet:avg')!

    expect(avg.data).toEqual([10, 20])
    expect(avg.label).toBe('Label:totalNet')
  })

  it('the max dataset fills toward the previous dataset (min) to render a shaded band', () => {
    const result = buildBandChartSeries(makePeriods(), makeBand(), ['totalNet'], labelFor, ['#3b82f6'])
    const max = result.find((d) => d.key === 'totalNet:max')!

    expect(max.fill).toBe('-1')
    expect(max.backgroundColor).toBe(withAlpha('#3b82f6', 0.18))
  })

  it('assigns one color per selected key, cycling through the palette, reused across its 3 datasets', () => {
    const result = buildBandChartSeries(
      makePeriods(),
      makeBand(),
      ['totalNet', 'totalAvailable'],
      labelFor,
      ['#3b82f6', '#ec4899'],
    )

    const netAvg = result.find((d) => d.key === 'totalNet:avg')!
    const availableAvg = result.find((d) => d.key === 'totalAvailable:avg')!
    expect(netAvg.borderColor).toBe('#3b82f6')
    expect(availableAvg.borderColor).toBe('#ec4899')
  })
})

describe('buildLineSeries (DASH-4/5/6)', () => {
  function periods(): PeriodSeries[] {
    return [
      { periodId: 'p1', cycleId: 'c1', periodStart: '2026-01-01', defaultCurrencyId: 'usd' },
      { periodId: 'p2', cycleId: 'c1', periodStart: '2026-02-01', defaultCurrencyId: 'usd' },
    ]
  }

  function rows(): BudgetLineSeriesRow[] {
    return [
      { budgetLineId: 'l1', budgetLineName: 'Groceries', periodId: 'p1', budgetedAmount: 100, netTotal: 80 },
      { budgetLineId: 'l1', budgetLineName: 'Groceries', periodId: 'p2', budgetedAmount: 100, netTotal: 90 },
      { budgetLineId: 'l2', budgetLineName: 'Rent', periodId: 'p1', budgetedAmount: 500, netTotal: 500 },
      { budgetLineId: 'l2', budgetLineName: 'Rent', periodId: 'p2', budgetedAmount: 500, netTotal: 500 },
    ]
  }

  it('produces 2 datasets per selected BudgetLine — budgeted and net — in period order', () => {
    const result = buildLineSeries(rows(), periods(), ['l1'], ['#3b82f6'])

    expect(result).toHaveLength(2)
    expect(result[0]).toMatchObject({ key: 'l1:budgeted', label: 'Groceries (budgeted)', data: [100, 100] })
    expect(result[1]).toMatchObject({ key: 'l1:net', label: 'Groceries (net)', data: [80, 90] })
  })

  it('maps multiple selected BudgetLines independently, each with its own name and color', () => {
    const result = buildLineSeries(rows(), periods(), ['l1', 'l2'], ['#3b82f6', '#ec4899'])

    const l1Budgeted = result.find((d) => d.key === 'l1:budgeted')!
    const l2Budgeted = result.find((d) => d.key === 'l2:budgeted')!
    expect(l1Budgeted.borderColor).toBe('#3b82f6')
    expect(l2Budgeted.label).toBe('Rent (budgeted)')
    expect(l2Budgeted.data).toEqual([500, 500])
    expect(l2Budgeted.borderColor).toBe('#ec4899')
  })

  it('fills 0 for a period a selected BudgetLine has no row for', () => {
    const sparseRows = rows().filter((r) => !(r.budgetLineId === 'l1' && r.periodId === 'p2'))

    const result = buildLineSeries(sparseRows, periods(), ['l1'], ['#3b82f6'])

    const budgeted = result.find((d) => d.key === 'l1:budgeted')!
    expect(budgeted.data).toEqual([100, 0])
  })

  it('returns an empty array when no BudgetLine is selected', () => {
    const result = buildLineSeries(rows(), periods(), [], ['#3b82f6'])

    expect(result).toEqual([])
  })
})
