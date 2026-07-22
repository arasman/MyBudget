// REQ-BL-MATRIX-1: BudgetMatrix date-coverage cell gating
import { describe, it, expect } from 'vitest'
import { isLineActiveForPeriod } from '../utils/isLineActiveForPeriod'
import type { BudgetLineResponse } from '@/features/budget-structure/types'
import type { PeriodSummary } from '@/features/budget-structure/types'

function makeLine(overrides: Partial<BudgetLineResponse> = {}): BudgetLineResponse {
  return {
    id: 'l1',
    name: 'Test Line',
    lineType: 'Expense',
    startDate: '2025-01-01' as any,
    endDate: null,
    budgetedAmount: 1000,
    currencyId: 'currency-gtq',
    categoryGroupId: 'g1',
    ...overrides,
  }
}

function makePeriod(startDate: string): PeriodSummary {
  return {
    id: 'p1',
    name: 'Period 1',
    periodNumber: 1,
    startDate: startDate as any,
    endDate: startDate as any,
    isClosed: false,
  }
}

describe('isLineActiveForPeriod (REQ-BL-MATRIX-1, AD-5)', () => {
  it('perpetual line covers any period', () => {
    const line = makeLine({ startDate: '2025-01-01' as any, endDate: null })
    const period = makePeriod('2030-06-01')
    expect(isLineActiveForPeriod(line, period)).toBe(true)
  })

  it('line starting before period start covers period', () => {
    const line = makeLine({ startDate: '2025-01-01' as any, endDate: null })
    const period = makePeriod('2025-06-01')
    expect(isLineActiveForPeriod(line, period)).toBe(true)
  })

  it('line starting ON period start covers period', () => {
    const line = makeLine({ startDate: '2025-06-01' as any, endDate: null })
    const period = makePeriod('2025-06-01')
    expect(isLineActiveForPeriod(line, period)).toBe(true)
  })

  it('line starting AFTER period start does NOT cover period', () => {
    const line = makeLine({ startDate: '2025-06-01' as any, endDate: null })
    const period = makePeriod('2025-03-01')
    expect(isLineActiveForPeriod(line, period)).toBe(false)
  })

  it('line with endDate before period start does NOT cover period', () => {
    const line = makeLine({ startDate: '2025-01-01' as any, endDate: '2025-02-28' as any })
    const period = makePeriod('2025-03-01')
    expect(isLineActiveForPeriod(line, period)).toBe(false)
  })

  it('line with endDate ON period start covers period', () => {
    const line = makeLine({ startDate: '2025-01-01' as any, endDate: '2025-03-01' as any })
    const period = makePeriod('2025-03-01')
    expect(isLineActiveForPeriod(line, period)).toBe(true)
  })

  it('line with endDate after period start covers period', () => {
    const line = makeLine({ startDate: '2025-01-01' as any, endDate: '2025-12-31' as any })
    const period = makePeriod('2025-06-01')
    expect(isLineActiveForPeriod(line, period)).toBe(true)
  })
})
