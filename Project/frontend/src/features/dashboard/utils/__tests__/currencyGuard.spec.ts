import { describe, it, expect } from 'vitest'
import { detectCurrencyMismatch } from '../currencyGuard'
import type { PeriodSeries } from '../../types/dashboard'

function period(defaultCurrencyId: string, periodId = 'p1'): PeriodSeries {
  return { periodId, cycleId: 'c1', periodStart: '2026-01-01', defaultCurrencyId }
}

describe('detectCurrencyMismatch (DASH-12)', () => {
  it('reports no mismatch for an empty period list', () => {
    const result = detectCurrencyMismatch([])

    expect(result).toEqual({ hasMismatch: false, currencyIds: [] })
  })

  it('reports no mismatch when every period shares the same defaultCurrencyId', () => {
    const periods = [period('usd', 'p1'), period('usd', 'p2'), period('usd', 'p3')]

    const result = detectCurrencyMismatch(periods)

    expect(result).toEqual({ hasMismatch: false, currencyIds: ['usd'] })
  })

  it('reports a mismatch when two periods carry different defaultCurrencyId values', () => {
    const periods = [period('usd', 'p1'), period('eur', 'p2')]

    const result = detectCurrencyMismatch(periods)

    expect(result.hasMismatch).toBe(true)
    expect(result.currencyIds).toEqual(['usd', 'eur'])
  })

  it('deduplicates currency ids across many periods sharing 2 distinct currencies', () => {
    const periods = [period('usd', 'p1'), period('eur', 'p2'), period('usd', 'p3'), period('eur', 'p4')]

    const result = detectCurrencyMismatch(periods)

    expect(result.hasMismatch).toBe(true)
    expect(result.currencyIds).toEqual(['usd', 'eur'])
  })
})
