import { describe, it, expect } from 'vitest'
import { resolvePeriodIds } from '../comparisonResolution'
import type { CycleOption } from '../../composables/useCycleOptions'

function cycles(): CycleOption[] {
  return [
    {
      id: 'c1',
      name: 'Cycle 1',
      defaultCurrencyId: 'usd',
      periods: [
        { id: 'p1', name: 'Period 1', startDate: '2026-01-01' },
        { id: 'p2', name: 'Period 2', startDate: '2026-02-01' },
      ],
    },
    {
      id: 'c2',
      name: 'Cycle 2',
      defaultCurrencyId: 'eur',
      periods: [
        { id: 'p3', name: 'Period 1', startDate: '2026-04-01' },
        { id: 'p4', name: 'Period 2', startDate: '2026-05-01' },
      ],
    },
  ]
}

describe('resolvePeriodIds (DASH-5/DASH-6 within-cycle vs cross-cycle resolution)', () => {
  it('within-cycle mode returns exactly the explicitly selected periodIds (DASH-5)', () => {
    const result = resolvePeriodIds(cycles(), { mode: 'within-cycle', cycleId: 'c1', periodIds: ['p1', 'p2'] })

    expect(result).toEqual(['p1', 'p2'])
  })

  it('within-cycle mode with no periods selected yet returns an empty array', () => {
    const result = resolvePeriodIds(cycles(), { mode: 'within-cycle', cycleId: 'c1', periodIds: [] })

    expect(result).toEqual([])
  })

  it('cross-cycle mode expands each selected cycleId to every one of its periodIds (DASH-6)', () => {
    const result = resolvePeriodIds(cycles(), { mode: 'cross-cycle', cycleIds: ['c1', 'c2'] })

    expect(result).toEqual(['p1', 'p2', 'p3', 'p4'])
  })

  it('cross-cycle mode with a single selected cycle only expands that one cycle', () => {
    const result = resolvePeriodIds(cycles(), { mode: 'cross-cycle', cycleIds: ['c2'] })

    expect(result).toEqual(['p3', 'p4'])
  })

  it('cross-cycle mode ignores cycleIds that do not match any known cycle', () => {
    const result = resolvePeriodIds(cycles(), { mode: 'cross-cycle', cycleIds: ['unknown'] })

    expect(result).toEqual([])
  })
})
