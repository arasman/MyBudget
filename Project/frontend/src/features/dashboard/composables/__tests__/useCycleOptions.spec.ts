import { describe, it, expect, vi, beforeEach } from 'vitest'

const { mockList, mockGet } = vi.hoisted(() => ({
  mockList: vi.fn(),
  mockGet: vi.fn(),
}))

vi.mock('@/features/budget-structure/api/cycles.api', () => ({
  list: mockList,
  get: mockGet,
}))

import { useCycleOptions } from '../useCycleOptions'

describe('useCycleOptions (supports DASH-5/DASH-6 cycle/period picker)', () => {
  beforeEach(() => {
    mockList.mockReset()
    mockGet.mockReset()
  })

  it('starts with an empty cycles list and loading false', () => {
    const { cycles, loading } = useCycleOptions()

    expect(cycles.value).toEqual([])
    expect(loading.value).toBe(false)
  })

  it('loads every cycle plus its periods, mapped into CycleOption shape with defaultCurrencyId', async () => {
    mockList.mockResolvedValue([
      { id: 'c1', name: 'Cycle 1', defaultCurrency: { id: 'usd', code: 'USD', symbol: '$' } },
      { id: 'c2', name: 'Cycle 2', defaultCurrency: { id: 'eur', code: 'EUR', symbol: '€' } },
    ])
    mockGet.mockImplementation((_budgetId: string, cycleId: string) =>
      Promise.resolve({
        id: cycleId,
        name: cycleId === 'c1' ? 'Cycle 1' : 'Cycle 2',
        periods: [{ id: `${cycleId}-p1`, name: 'Period 1', startDate: '2026-01-01' }],
      }),
    )

    const { cycles, load } = useCycleOptions()
    await load('budget-1')

    expect(mockList).toHaveBeenCalledWith('budget-1')
    expect(cycles.value).toEqual([
      { id: 'c1', name: 'Cycle 1', defaultCurrencyId: 'usd', periods: [{ id: 'c1-p1', name: 'Period 1', startDate: '2026-01-01' }] },
      { id: 'c2', name: 'Cycle 2', defaultCurrencyId: 'eur', periods: [{ id: 'c2-p1', name: 'Period 1', startDate: '2026-01-01' }] },
    ])
  })

  it('sets loading true while fetching and false once settled', async () => {
    mockList.mockResolvedValue([])
    const { loading, load } = useCycleOptions()

    const promise = load('budget-1')
    expect(loading.value).toBe(true)
    await promise
    expect(loading.value).toBe(false)
  })
})
